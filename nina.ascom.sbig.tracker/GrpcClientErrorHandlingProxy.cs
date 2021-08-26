using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Castle.DynamicProxy;
using Grpc.Core;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ASCOM.NINA.SBIGTracker {

    /// <summary>
    /// This class intercepts exceptions raised by the gRPC client to deserialize failures with an "Internal" status code to their real exceptions
    /// from the server
    /// </summary>
    /// <typeparam name="T">The client being proxied</typeparam>
    public class GrpcClientErrorHandlingProxy<T> : IAsyncInterceptor where T : class {
        private static readonly ProxyGenerator proxyGenerator = new ProxyGenerator();
        private readonly JsonSerializer jsonSerializer;

        public GrpcClientErrorHandlingProxy() {
            var settings = new JsonSerializerSettings {
                TypeNameHandling = TypeNameHandling.All,
                Context = new StreamingContext(StreamingContextStates.CrossAppDomain),
            };
            jsonSerializer = JsonSerializer.Create(settings);
        }

        public static T Wrap(T wrapped) {
            if (typeof(T).IsInterface) {
                return proxyGenerator.CreateInterfaceProxyWithTarget(wrapped, new GrpcClientErrorHandlingProxy<T>());
            } else {
                return proxyGenerator.CreateClassProxyWithTarget(wrapped, new GrpcClientErrorHandlingProxy<T>());
            }
        }

        public void InterceptAsynchronous(IInvocation invocation) {
            invocation.Proceed();
            Task result = (Task)invocation.ReturnValue;
            if (result.IsFaulted) {
                var translatedException = TranslateException(result.Exception);
                if (!Object.ReferenceEquals(translatedException, result.Exception)) {
                    invocation.ReturnValue = Task.FromException(translatedException);
                }
            }
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation) {
            invocation.Proceed();
            Task<TResult> result = (Task<TResult>)invocation.ReturnValue;
            if (result.IsFaulted) {
                var translatedException = TranslateException(result.Exception);
                if (!Object.ReferenceEquals(translatedException, result.Exception)) {
                    invocation.ReturnValue = Task.FromException<TResult>(translatedException);
                }
            }
        }

        public void InterceptSynchronous(IInvocation invocation) {
            try {
                invocation.Proceed();
            } catch (AggregateException e) {
                var innerException = e.InnerException;
                var translatedException = TranslateException(innerException);
                if (Object.ReferenceEquals(innerException, translatedException)) {
                    throw;
                } else {
                    throw translatedException;
                }
            }
        }

        private Exception TranslateException(Exception e) {
            if (e is RpcException) {
                var rpcException = (RpcException)e;
                if (rpcException.StatusCode == StatusCode.Internal) {
                    return TryDeserializeExceptionString(rpcException.Status.Detail);
                }
            }
            return e;
        }

        private Exception TryDeserializeExceptionString(string detail) {
            try {
                using (var sr = new StringReader(detail)) {
                    var jr = new JsonTextReader(sr);
                    var deserialized = (JObject)jsonSerializer.Deserialize(jr);

                    // Set the RemoteStackTrace so it gets combined with the local stack trace upon being thrown
                    var stackTraceString = deserialized.GetValue("StackTraceString").ToString() + Environment.NewLine;
                    deserialized["RemoteStackTraceString"] = new JValue(stackTraceString);
                    var className = deserialized.GetValue("ClassName").ToString();
                    var type = getTypeByFullName(className);
                    if (type == null) {
                        return null;
                    }
                    return (Exception)deserialized.ToObject(type);
                }
            } catch (Exception) {
                return null;
            }
        }

        private static Type getTypeByFullName(string classFullName) {
            List<Type> returnVal = new List<Type>();

            foreach (Assembly a in AppDomain.CurrentDomain.GetAssemblies()) {
                Type[] assemblyTypes = a.GetTypes();
                for (int j = 0; j < assemblyTypes.Length; j++) {
                    if (assemblyTypes[j].FullName == classFullName) {
                        returnVal.Add(assemblyTypes[j]);
                    }
                }
            }

            if (returnVal.Count != 1) {
                return null;
            }
            return returnVal.First();
        }
    }
}

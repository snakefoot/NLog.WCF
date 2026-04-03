//
// Copyright (c) 2004-2025 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
//
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions
// are met:
//
// * Redistributions of source code must retain the above copyright notice,
//   this list of conditions and the following disclaimer.
//
// * Redistributions in binary form must reproduce the above copyright notice,
//   this list of conditions and the following disclaimer in the documentation
//   and/or other materials provided with the distribution.
//
// * Neither the name of Jaroslaw Kowalski nor the names of its
//   contributors may be used to endorse or promote products derived from this
//   software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
// AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
// IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE
// ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE
// LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
// CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF
// SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
// INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN
// CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE)
// ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF
// THE POSSIBILITY OF SUCH DAMAGE.
//

namespace NLog.Wcf.Tests
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Net;
    using System.ServiceModel;
    using System.ServiceModel.Channels;
    using System.ServiceModel.Description;
    using System.Threading;
    using NLog.Common;
    using NLog.LogReceiverService;
    using NLog.Targets;
    using NLog.Targets.Wrappers;
    using Xunit;

    public class LogReceiverWebServiceTargetTests
    {
        public LogReceiverWebServiceTargetTests()
        {
            LogManager.ThrowExceptions = true;
        }

        [Theory]
        [InlineData("http://notimportant:9999/", BasicHttpSecurityMode.TransportCredentialOnly)]
        [InlineData("https://notimportant:9999/", BasicHttpSecurityMode.Transport)]
        public void CreateLogReceiverWithExpectedBasicHttpBindingSecurityMode(string endpoint, BasicHttpSecurityMode expectedMode)
        {
            var target = new TestLogReceiverWebServiceTarget
            {
                UseBinaryEncoding = false
            };

            var client = target.CreateClient(endpoint);
            var binding = Assert.IsType<BasicHttpBinding>(client.Endpoint.Binding);

            Assert.Equal(HttpClientCredentialType.Windows, binding.Security.Transport.ClientCredentialType);
            Assert.Equal(expectedMode, binding.Security.Mode);
        }

        [Theory]
        [InlineData("http://notimportant:9999/", typeof(HttpTransportBindingElement))]
        [InlineData("https://notimportant:9999/", typeof(HttpsTransportBindingElement))]
        public void CreateLogReceiverWithExpectedCustomBindingTransportElement(string endpoint, Type expectedTransport)
        {
            var target = new TestLogReceiverWebServiceTarget
            {
                UseBinaryEncoding = true
            };

            var client = target.CreateClient(endpoint);
            var binding = Assert.IsType<CustomBinding>(client.Endpoint.Binding);

            Assert.IsType<BinaryMessageEncodingBindingElement>(binding.Elements[0]);
            Assert.IsType(expectedTransport, binding.Elements[binding.Elements.Count - 1]);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("message1")]
        public void TranslateEventAndBack(string message)
        {
            // Arrange
            var service = new LogReceiverWebServiceTarget { IncludeEventProperties = true };

            var logEvent = new LogEventInfo(LogLevel.Debug, "logger1", message);

            var nLogEvents = new NLogEvents
            {
                Strings = new StringCollection(),
                LayoutNames = new StringCollection(),
                BaseTimeUtc = DateTime.UtcNow.Ticks,
                ClientName = "client1",
                Events = new NLogEvent[0]
            };
            var dict2 = new Dictionary<string, int>();

            // Act
            var translateEvent = service.TranslateEvent(logEvent, nLogEvents, dict2);
            var result = translateEvent.ToEventInfo(nLogEvents, nLogEvents.Strings[translateEvent.LoggerOrdinal]);

            // Assert
            Assert.Equal("logger1", result.LoggerName);
            Assert.Equal(message, result.Message);
        }

        [Fact]
        public void LogReceiverWebServiceTargetSendSingleEventTest()
        {
            var target = new MockLogReceiverWebServiceTarget();
            target.EndpointAddress = "http://notimportant:9999/";
            target.Parameters.Add(new MethodCallParameter("message", "${message}"));
            target.Parameters.Add(new MethodCallParameter("lvl", "${level}"));

            var logFactory = new LogFactory().Setup().LoadConfiguration(cfg =>
            {
                cfg.ForLogger().WriteTo(target);
            }).LogFactory;

            var logger = logFactory.GetLogger("loggerName");
            logger.Info("message text");
            logFactory.Flush();

            var payload = target.LastPayload;
            Assert.NotNull(payload);
            Assert.Equal(2, payload.LayoutNames.Count);
            Assert.Equal("message", payload.LayoutNames[0]);
            Assert.Equal("lvl", payload.LayoutNames[1]);
            Assert.Equal(3, payload.Strings.Count);
            Assert.Single(payload.Events);
            Assert.Equal("message text", payload.Strings[payload.Events[0].ValueIndexes[0]]);
            Assert.Equal("Info", payload.Strings[payload.Events[0].ValueIndexes[1]]);
            Assert.Equal("loggerName", payload.Strings[payload.Events[0].LoggerOrdinal]);
            Assert.NotNull(target.LastReceiverClient);
            Assert.False(target.LastReceiverClient.OpenSignaled);   // Mock never opens
            Assert.False(target.LastReceiverClient.CloseSignaled);  // Only close when opened
            Assert.False(target.LastReceiverClient.AbortSignaled);  // Only abort when close fails
            Assert.True(target.LastReceiverClient.DisposeSignaled); // EventHandlers disconnected from target
        }

        [Fact]
        public void LogReceiverWebServiceTargetSingleEventTest()
        {
            var target = new NoSendLogReceiverWebServiceTarget();
            target.EndpointAddress = "http://notimportant:9999/";
            target.Parameters.Add(new MethodCallParameter("message", "${message}"));
            target.Parameters.Add(new MethodCallParameter("lvl", "${level}"));

            var logger = new LogFactory().Setup().LoadConfiguration(cfg =>
            {
                cfg.ForLogger().WriteTo(target);
            }).GetLogger("loggerName");

            logger.Info("message text");

            var payload = target.LastPayload;
            Assert.Equal(2, payload.LayoutNames.Count);
            Assert.Equal("message", payload.LayoutNames[0]);
            Assert.Equal("lvl", payload.LayoutNames[1]);
            Assert.Equal(3, payload.Strings.Count);
            Assert.Single(payload.Events);
            Assert.Equal("message text", payload.Strings[payload.Events[0].ValueIndexes[0]]);
            Assert.Equal("Info", payload.Strings[payload.Events[0].ValueIndexes[1]]);
            Assert.Equal("loggerName", payload.Strings[payload.Events[0].LoggerOrdinal]);
        }

        [Fact]
        public void LogReceiverWebServiceTargetMultipleEventTest()
        {
            var target = new NoSendLogReceiverWebServiceTarget();
            target.EndpointAddress = "http://notimportant:9999/";
            target.Parameters.Add(new MethodCallParameter("message", "${message}"));
            target.Parameters.Add(new MethodCallParameter("lvl", "${level}"));

            new LogFactory().Setup().LoadConfiguration(cfg =>
            {
                cfg.ForLogger().WriteTo(target);
            });

            var exceptions = new List<Exception>();

            var events = new[]
            {
                LogEventInfo.Create(LogLevel.Info, "logger1", "message1").WithContinuation(exceptions.Add),
                LogEventInfo.Create(LogLevel.Debug, "logger2", "message2").WithContinuation(exceptions.Add),
                LogEventInfo.Create(LogLevel.Fatal, "logger1", "message2").WithContinuation(exceptions.Add),
            };

            target.WriteAsyncLogEvents(events);

            // with multiple events, we should get string caching
            var payload = target.LastPayload;
            Assert.Equal(2, payload.LayoutNames.Count);
            Assert.Equal("message", payload.LayoutNames[0]);
            Assert.Equal("lvl", payload.LayoutNames[1]);

            // 7 strings instead of 9 since 'logger1' and 'message2' are being reused
            Assert.Equal(7, payload.Strings.Count);

            Assert.Equal(3, payload.Events.Length);
            Assert.Equal("message1", payload.Strings[payload.Events[0].ValueIndexes[0]]);
            Assert.Equal("message2", payload.Strings[payload.Events[1].ValueIndexes[0]]);
            Assert.Equal("message2", payload.Strings[payload.Events[2].ValueIndexes[0]]);

            Assert.Equal("Info", payload.Strings[payload.Events[0].ValueIndexes[1]]);
            Assert.Equal("Debug", payload.Strings[payload.Events[1].ValueIndexes[1]]);
            Assert.Equal("Fatal", payload.Strings[payload.Events[2].ValueIndexes[1]]);

            Assert.Equal("logger1", payload.Strings[payload.Events[0].LoggerOrdinal]);
            Assert.Equal("logger2", payload.Strings[payload.Events[1].LoggerOrdinal]);
            Assert.Equal("logger1", payload.Strings[payload.Events[2].LoggerOrdinal]);

            Assert.Equal(payload.Events[0].LoggerOrdinal, payload.Events[2].LoggerOrdinal);
        }

        [Fact]
        public void LogReceiverWebServiceTargetMultipleEventWithPerEventPropertiesTest()
        {
            var target = new NoSendLogReceiverWebServiceTarget();
            target.IncludeEventProperties = true;
            target.EndpointAddress = "http://notimportant:9999/";
            target.Parameters.Add(new MethodCallParameter("message", "${message}"));
            target.Parameters.Add(new MethodCallParameter("lvl", "${level}"));

            new LogFactory().Setup().LoadConfiguration(cfg =>
            {
                cfg.ForLogger().WriteTo(target);
            });

            var exceptions = new List<Exception>();

            var events = new[]
            {
                LogEventInfo.Create(LogLevel.Info, "logger1", "message1").WithContinuation(exceptions.Add),
                LogEventInfo.Create(LogLevel.Debug, "logger2", "message2").WithContinuation(exceptions.Add),
                LogEventInfo.Create(LogLevel.Fatal, "logger1", "message2").WithContinuation(exceptions.Add),
            };

            events[0].LogEvent.Properties["prop1"] = "value1";
            events[1].LogEvent.Properties["prop1"] = "value2";
            events[2].LogEvent.Properties["prop1"] = "value3";
            events[0].LogEvent.Properties["prop2"] = "value2a";

            target.WriteAsyncLogEvents(events);

            // with multiple events, we should get string caching
            var payload = target.LastPayload;

            // 4 layout names - 2 from Parameters, 2 from unique properties in events
            Assert.Equal(4, payload.LayoutNames.Count);
            Assert.Equal("message", payload.LayoutNames[0]);
            Assert.Equal("lvl", payload.LayoutNames[1]);
            Assert.Equal("prop1", payload.LayoutNames[2]);
            Assert.Equal("prop2", payload.LayoutNames[3]);

            Assert.Equal(12, payload.Strings.Count);

            Assert.Equal(3, payload.Events.Length);
            Assert.Equal("message1", payload.Strings[payload.Events[0].ValueIndexes[0]]);
            Assert.Equal("message2", payload.Strings[payload.Events[1].ValueIndexes[0]]);
            Assert.Equal("message2", payload.Strings[payload.Events[2].ValueIndexes[0]]);

            Assert.Equal("Info", payload.Strings[payload.Events[0].ValueIndexes[1]]);
            Assert.Equal("Debug", payload.Strings[payload.Events[1].ValueIndexes[1]]);
            Assert.Equal("Fatal", payload.Strings[payload.Events[2].ValueIndexes[1]]);

            Assert.Equal("value1", payload.Strings[payload.Events[0].ValueIndexes[2]]);
            Assert.Equal("value2", payload.Strings[payload.Events[1].ValueIndexes[2]]);
            Assert.Equal("value3", payload.Strings[payload.Events[2].ValueIndexes[2]]);

            Assert.Equal("value2a", payload.Strings[payload.Events[0].ValueIndexes[3]]);
            Assert.Equal("", payload.Strings[payload.Events[1].ValueIndexes[3]]);
            Assert.Equal("", payload.Strings[payload.Events[2].ValueIndexes[3]]);

            Assert.Equal("logger1", payload.Strings[payload.Events[0].LoggerOrdinal]);
            Assert.Equal("logger2", payload.Strings[payload.Events[1].LoggerOrdinal]);
            Assert.Equal("logger1", payload.Strings[payload.Events[2].LoggerOrdinal]);

            Assert.Equal(payload.Events[0].LoggerOrdinal, payload.Events[2].LoggerOrdinal);
        }

        [Fact]
        public void NoEmptyEventLists()
        {
            var target = new NoSendLogReceiverWebServiceTarget();
            target.EndpointAddress = "http://notimportant:9999/";

            var logger = new LogFactory().Setup().LoadConfiguration(cfg =>
            {
                var asyncTarget = new AsyncTargetWrapper(target)
                {
                    Name = "NoEmptyEventLists_wrapper"
                };
                cfg.ForLogger().WriteTo(asyncTarget);
            }).GetLogger("logger1");

            try
            {
                logger.Info("message1");
                Assert.True(target.SendCompleted.Wait(10000));
                Assert.Equal(1, target.SendCount);
            }
            finally
            {
                logger.Factory.Shutdown();
            }
        }

        private sealed class TestLogReceiverWebServiceTarget : LogReceiverWebServiceTarget
        {
            public WcfLogReceiverClient CreateClient(string endpoint)
            {
                return (WcfLogReceiverClient)CreateLogReceiver(endpoint);
            }
        }

        public sealed class NoSendLogReceiverWebServiceTarget : LogReceiverWebServiceTarget
        {
            public NLogEvents LastPayload;
            public int SendCount;

            public ManualResetEventSlim SendCompleted = new ManualResetEventSlim(false);

            public NoSendLogReceiverWebServiceTarget() : base()
            {
            }

            public NoSendLogReceiverWebServiceTarget(string name) : base(name)
            {
            }

            protected internal override bool OnSend(NLogEvents events, IEnumerable<AsyncLogEventInfo> asyncContinuations)
            {
                LastPayload = events;
                ++SendCount;

                foreach (var ac in asyncContinuations)
                {
                    ac.Continuation(null);
                }

                SendCompleted.Set();
                return false;   // Never send
            }
        }

        public sealed class MockLogReceiverWebServiceTarget : LogReceiverWebServiceTarget
        {

            public NLogEvents LastPayload;
            public MockWcfLogReceiverClient LastReceiverClient;

            protected override IWcfLogReceiverClient CreateLogReceiver(string endPointAddress)
            {
                var client = new MockWcfLogReceiverClient(this);
                client.ProcessLogMessagesCompleted += ClientOnProcessLogMessagesCompleted;
                LastReceiverClient = client;
                return client;
            }

            private static void ClientOnProcessLogMessagesCompleted(object sender, AsyncCompletedEventArgs asyncCompletedEventArgs)
            {
                if (sender is IDisposable disposable)
                {
                    // Attempt to "disconnect" the client-event-handlers to avoid "leak"
                    disposable.Dispose();
                }
                else if (sender is IWcfLogReceiverClient client)
                {
                    if (client.State == CommunicationState.Opened)
                    {
                        try
                        {
                            client.Close();
                        }
                        catch
                        {
                            client.Abort();
                        }
                    }
                }
            }

            public sealed class MockWcfLogReceiverClient : IWcfLogReceiverClient, IDisposable
            {
                private readonly MockLogReceiverWebServiceTarget _target;

                public MockWcfLogReceiverClient(MockLogReceiverWebServiceTarget target)
                {
                    _target = target;
                }

                public bool OpenSignaled { get; private set; }
                public bool CloseSignaled { get; private set; }
                public bool AbortSignaled { get; private set; }
                public bool DisposeSignaled { get; private set; }


                public EventHandler<AsyncCompletedEventArgs> ProcessLogMessagesCompleted;

                public EventHandler<AsyncCompletedEventArgs> OpenCompleted;

                public EventHandler<AsyncCompletedEventArgs> CloseCompleted;

                public event EventHandler Closed;

                public event EventHandler Closing;

                public event EventHandler Faulted;

                public event EventHandler Opened;

                public event EventHandler Opening;

                ClientCredentials IWcfLogReceiverClient.ClientCredentials => null;
                IClientChannel IWcfLogReceiverClient.InnerChannel => null;

                ServiceEndpoint IWcfLogReceiverClient.Endpoint => null;

#if NETFRAMEWORK
                CookieContainer IWcfLogReceiverClient.CookieContainer { get => null; set => throw new NotImplementedException(); }
#endif
                CommunicationState ICommunicationObject.State => OpenSignaled && !CloseSignaled && !AbortSignaled ? CommunicationState.Opened : CommunicationState.Closed;

                event EventHandler<AsyncCompletedEventArgs> IWcfLogReceiverClient.ProcessLogMessagesCompleted
                {
                    add
                    {
                        ProcessLogMessagesCompleted += value;
                    }

                    remove
                    {
                        ProcessLogMessagesCompleted -= value;
                    }
                }

                event EventHandler<AsyncCompletedEventArgs> IWcfLogReceiverClient.OpenCompleted
                {
                    add
                    {
                        OpenCompleted += value;
                    }

                    remove
                    {
                        OpenCompleted -= value;
                    }
                }

                event EventHandler<AsyncCompletedEventArgs> IWcfLogReceiverClient.CloseCompleted
                {
                    add
                    {
                        CloseCompleted += value;
                    }

                    remove
                    {
                        CloseCompleted -= value;
                    }
                }

                void ICommunicationObject.Abort() => AbortSignaled = true;

                IAsyncResult ICommunicationObject.BeginClose(AsyncCallback callback, object state)
                {
                    throw new NotImplementedException();
                }

                IAsyncResult ICommunicationObject.BeginClose(TimeSpan timeout, AsyncCallback callback, object state)
                {
                    throw new NotImplementedException();
                }

                IAsyncResult ICommunicationObject.BeginOpen(AsyncCallback callback, object state)
                {
                    throw new NotImplementedException();
                }

                IAsyncResult ICommunicationObject.BeginOpen(TimeSpan timeout, AsyncCallback callback, object state)
                {
                    throw new NotImplementedException();
                }

                IAsyncResult IWcfLogReceiverClient.BeginProcessLogMessages(NLogEvents events, AsyncCallback callback, object asyncState)
                {
                    throw new NotImplementedException();
                }

                public void Fault()
                {
                    Faulted?.Invoke(this, EventArgs.Empty);
                }

                public void Close()
                {
                    Closing?.Invoke(this, EventArgs.Empty);
                    CloseSignaled = true;
                    Closed?.Invoke(this, EventArgs.Empty);
                }

                void ICommunicationObject.Close(TimeSpan timeout)
                {
                    Close();
                }

                void IWcfLogReceiverClient.CloseAsync()
                {
                    Close();
                }

                void IWcfLogReceiverClient.CloseAsync(object userState)
                {
                    Close();
                }

#if NETFRAMEWORK
                void IWcfLogReceiverClient.DisplayInitializationUI()
                {
                }
#endif

                void ICommunicationObject.EndClose(IAsyncResult result)
                {
                    CloseSignaled = true;
                }

                void ICommunicationObject.EndOpen(IAsyncResult result)
                {
                    OpenSignaled = true;
                }

                void IWcfLogReceiverClient.EndProcessLogMessages(IAsyncResult result)
                {
                }

                public void Open()
                {
                    Opening?.Invoke(this, EventArgs.Empty);
                    OpenSignaled = true;
                    Opened?.Invoke(this, EventArgs.Empty);
                }

                void ICommunicationObject.Open(TimeSpan timeout)
                {
                    Open();
                }

                void IWcfLogReceiverClient.OpenAsync()
                {
                    Open();
                }

                void IWcfLogReceiverClient.OpenAsync(object userState)
                {
                    Open();
                }

                void IWcfLogReceiverClient.ProcessLogMessagesAsync(NLogEvents events)
                {
                    _target.LastPayload = events;
                    ThreadPool.QueueUserWorkItem(s => ProcessLogMessagesCompleted?.Invoke(this, new AsyncCompletedEventArgs(null, false, null)));
                }

                void IWcfLogReceiverClient.ProcessLogMessagesAsync(NLogEvents events, object userState)
                {
                    _target.LastPayload = events;
                    ThreadPool.QueueUserWorkItem(s => ProcessLogMessagesCompleted?.Invoke(this, new AsyncCompletedEventArgs(null, false, userState)));
                }

                public void Dispose()
                {
                    if (OpenSignaled)
                    {
                        CloseSignaled = true;
                    }
                    // Disconnect from target to avoid "leak"
                    ProcessLogMessagesCompleted = null;
                    OpenCompleted = null;
                    CloseCompleted = null;
                    Closed = null;
                    Closing = null;
                    Faulted = null;
                    Opened = null;
                    Opening = null;
                    DisposeSignaled = true;
                }
            }
        }
    }
}

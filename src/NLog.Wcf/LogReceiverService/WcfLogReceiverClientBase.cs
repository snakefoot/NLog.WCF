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

namespace NLog.LogReceiverService
{
    using System;
    using System.ComponentModel;
    using System.Net;
    using System.ServiceModel;
    using System.ServiceModel.Channels;

    /// <summary>
    /// Abstract base class for the WcfLogReceiverXXXWay classes.  It can only be
    /// used internally (see internal constructor).  It passes off any Channel usage
    /// to the inheriting class.
    /// </summary>
    /// <typeparam name="TService">Type of the WCF service.</typeparam>
    public abstract class WcfLogReceiverClientBase<TService> : ClientBase<TService>, IWcfLogReceiverClient, IDisposable
        where TService : class
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        protected WcfLogReceiverClientBase()
            : base()
        {
        }

#if NETFRAMEWORK
        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        /// <param name="endpointConfigurationName">Name of the endpoint configuration.</param>
        protected WcfLogReceiverClientBase(string endpointConfigurationName)
            : base(endpointConfigurationName)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        /// <param name="endpointConfigurationName">Name of the endpoint configuration.</param>
        /// <param name="remoteAddress">The remote address.</param>
        protected WcfLogReceiverClientBase(string endpointConfigurationName, string remoteAddress)
            : base(endpointConfigurationName, remoteAddress)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        /// <param name="endpointConfigurationName">Name of the endpoint configuration.</param>
        /// <param name="remoteAddress">The remote address.</param>
        protected WcfLogReceiverClientBase(string endpointConfigurationName, EndpointAddress remoteAddress)
            : base(endpointConfigurationName, remoteAddress)
        {
        }
#endif

        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        /// <param name="binding">The binding.</param>
        /// <param name="remoteAddress">The remote address.</param>
        protected WcfLogReceiverClientBase(Binding binding, EndpointAddress remoteAddress)
            : base(binding, remoteAddress)
        {
        }

#if !NET35
        /// <summary>
        /// Initializes a new instance of the <see cref="WcfLogReceiverClientBase{TService}"/> class.
        /// </summary>
        /// <param name="endpoint">The endpoint for a service that allows clients to find and communicate with the service.</param>
        protected WcfLogReceiverClientBase(System.ServiceModel.Description.ServiceEndpoint endpoint)
            : base(endpoint)
        {
        }
#endif

        /// <summary>
        /// Occurs when the log message processing has completed.
        /// </summary>
        public event EventHandler<AsyncCompletedEventArgs>? ProcessLogMessagesCompleted;

        /// <summary>
        /// Occurs when Open operation has completed.
        /// </summary>
        public event EventHandler<AsyncCompletedEventArgs>? OpenCompleted;

        /// <summary>
        /// Occurs when Close operation has completed.
        /// </summary>
        public event EventHandler<AsyncCompletedEventArgs>? CloseCompleted;

#if NETFRAMEWORK && !NET35

        /// <summary>
        /// Gets or sets the cookie container.
        /// </summary>
        /// <value>The cookie container.</value>
        public CookieContainer? CookieContainer
        {
            get
            {
                var httpCookieContainerManager = InnerChannel.GetProperty<IHttpCookieContainerManager>();
                return httpCookieContainerManager?.CookieContainer;
            }
            set
            {
                var httpCookieContainerManager = InnerChannel.GetProperty<IHttpCookieContainerManager>();
                if (httpCookieContainerManager != null)
                {
                    httpCookieContainerManager.CookieContainer = value;
                }
                else
                {
                    throw new InvalidOperationException("Unable to set the CookieContainer. Please make sure the binding contains an HttpCookieContainerBindingElement.");
                }
            }
        }

#endif

        /// <summary>
        /// Opens the client asynchronously.
        /// </summary>
        public void OpenAsync()
        {
            OpenAsync(null);
        }

        /// <summary>
        /// Opens the client asynchronously.
        /// </summary>
        /// <param name="userState">User-specific state.</param>
        public void OpenAsync(object? userState)
        {
            InvokeAsync(OnBeginOpen, null, OnEndOpen, OnOpenCompleted, userState);
        }

        /// <summary>
        /// Closes the client asynchronously.
        /// </summary>
        public void CloseAsync()
        {
            CloseAsync(null);
        }

        /// <summary>
        /// Closes the client asynchronously.
        /// </summary>
        /// <param name="userState">User-specific state.</param>
        public void CloseAsync(object? userState)
        {
            InvokeAsync(OnBeginClose, null, OnEndClose, OnCloseCompleted, userState);
        }

        /// <summary>
        /// Implement IDisposable pattern
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Implement IDisposable pattern
        /// </summary>
        /// <param name="disposing">Disposing managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    if (State == CommunicationState.Opened)
                        Close();
                }
                catch
                {
                    Abort();
                }
                finally
                {
                    ProcessLogMessagesCompleted = null;
                    OpenCompleted = null;
                    CloseCompleted = null;
                }
            }
        }

        /// <summary>
        /// Processes the log messages asynchronously.
        /// </summary>
        /// <param name="events">The events to send.</param>
        public void ProcessLogMessagesAsync(NLogEvents events)
        {
            ProcessLogMessagesAsync(events, null);
        }

        /// <summary>
        /// Processes the log messages asynchronously.
        /// </summary>
        /// <param name="events">The events to send.</param>
        /// <param name="userState">User-specific state.</param>
        public void ProcessLogMessagesAsync(NLogEvents events, object? userState)
        {
            InvokeAsync(
                OnBeginProcessLogMessages,
                new object[] { events },
                OnEndProcessLogMessages,
                OnProcessLogMessagesCompleted,
                userState);
        }

        /// <summary>
        /// Begins processing of log messages.
        /// </summary>
        /// <param name="events">The events to send.</param>
        /// <param name="callback">The callback.</param>
        /// <param name="asyncState">Asynchronous state.</param>
        /// <returns>
        /// IAsyncResult value which can be passed to <see cref="ILogReceiverOneWayClient.EndProcessLogMessages"/>.
        /// </returns>
        public abstract IAsyncResult BeginProcessLogMessages(NLogEvents events, AsyncCallback callback, object asyncState);

        /// <summary>
        /// Ends asynchronous processing of log messages.
        /// </summary>
        /// <param name="result">The result.</param>
        public abstract void EndProcessLogMessages(IAsyncResult result);

        private IAsyncResult OnBeginProcessLogMessages(object[] inValues, AsyncCallback callback, object asyncState)
        {
            var events = (NLogEvents)inValues[0];
            return BeginProcessLogMessages(events, callback, asyncState);
        }

        private object[]? OnEndProcessLogMessages(IAsyncResult result)
        {
            EndProcessLogMessages(result);
            return null;
        }

        private void OnProcessLogMessagesCompleted(object state)
        {
            var processLogMessagesCompleted = ProcessLogMessagesCompleted;
            if (processLogMessagesCompleted != null)
            {
                var e = (InvokeAsyncCompletedEventArgs)state;

                processLogMessagesCompleted(this, new AsyncCompletedEventArgs(e.Error, e.Cancelled, e.UserState));
            }
        }

        private IAsyncResult OnBeginOpen(object[] inValues, AsyncCallback callback, object asyncState)
        {
            return ((ICommunicationObject)this).BeginOpen(callback, asyncState);
        }

        private object[]? OnEndOpen(IAsyncResult result)
        {
            ((ICommunicationObject)this).EndOpen(result);
            return null;
        }

        private void OnOpenCompleted(object state)
        {
            var openCompleted = OpenCompleted;
            if (openCompleted != null)
            {
                var e = (InvokeAsyncCompletedEventArgs)state;

                openCompleted(this, new AsyncCompletedEventArgs(e.Error, e.Cancelled, e.UserState));
            }
        }

        private IAsyncResult OnBeginClose(object[] inValues, AsyncCallback callback, object asyncState)
        {
            return ((ICommunicationObject)this).BeginClose(callback, asyncState);
        }

        private object[]? OnEndClose(IAsyncResult result)
        {
            ((ICommunicationObject)this).EndClose(result);
            return null;
        }

        private void OnCloseCompleted(object state)
        {
            var closeCompleted = CloseCompleted;
            if (closeCompleted != null)
            {
                var e = (InvokeAsyncCompletedEventArgs)state;

                closeCompleted(this, new AsyncCompletedEventArgs(e.Error, e.Cancelled, e.UserState));
            }
        }
    }
}

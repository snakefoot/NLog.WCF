// 
// Copyright (c) 2004-2021 Jaroslaw Kowalski <jaak@jkowalski.net>, Kim Christensen, Julian Verdurmen
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
    /// <summary>
    /// Base implementation of a log receiver server which forwards received logs through <see cref="LogManager"/> or a given <see cref="LogFactory"/>.
    /// </summary>
    public abstract class BaseLogReceiverForwardingService
    {
        private readonly LogFactory _logFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseLogReceiverForwardingService"/> class.
        /// </summary>
        protected BaseLogReceiverForwardingService()
            : this(LogManager.LogFactory)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BaseLogReceiverForwardingService"/> class.
        /// </summary>
        /// <param name="logFactory">The log factory.</param>
        protected BaseLogReceiverForwardingService(LogFactory logFactory)
        {
            _logFactory = logFactory ?? LogManager.LogFactory;
        }

        /// <summary>
        /// Processes the log messages.
        /// </summary>
        /// <param name="events">The events to process.</param>
        public void ProcessLogMessages(NLogEvents events)
        {
            var logEvents = events.ToEventInfoArray(string.Empty);
            if (!string.IsNullOrEmpty(events.ClientName))
            {
                foreach (var logEvent in logEvents)
                {
                    logEvent.Properties["ClientName"] = events.ClientName;
                }
            }
            ProcessLogMessages(logEvents);
        }

        /// <summary>
        /// Processes the log messages.
        /// </summary>
        /// <param name="logEvents">The log events.</param>
        protected virtual void ProcessLogMessages(LogEventInfo[] logEvents)
        {
            Logger? logger = null;
            string lastLoggerName = string.Empty;

            foreach (var ev in logEvents)
            {
                if (ev.LoggerName != lastLoggerName)
                {
                    logger = _logFactory.GetLogger(ev.LoggerName);
                    lastLoggerName = ev.LoggerName;
                }

                logger?.Log(ev);
            }
        }
    }
}
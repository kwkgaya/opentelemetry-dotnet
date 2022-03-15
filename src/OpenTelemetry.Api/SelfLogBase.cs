// <copyright file="OpenTelemetryApiEventSource.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

#if XAMARIN
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Reflection;

namespace OpenTelemetry
{
    public class SelfLogBase
    {
        public static Action<SelfLogEventArgs> Listener;

        private Lazy<string> sourceLazy;

        protected SelfLogBase()
        {
            this.sourceLazy = new Lazy<string>(() => this.GetSource());
        }

        protected virtual string Source => sourceLazy.Value;

        protected void WriteEvent(int eventId, params object[] args)
        {
            if (Listener == null)
            {
                return;
            }

            try
            {
                // TODO: Does this work on mono?
                // If not, we can use caller information, but then cannot use the params argument
                var method = new System.Diagnostics.StackTrace().GetFrame(1).GetMethod();
                var @event = GetEventAttribute(method);
                var log = string.Format(@event.Message, args);
                var eventData = new SelfLogEventArgs() { EventId = @event.EventId, EventSource = this.Source, Level = @event.Level, Message = log };
                Listener?.Invoke(eventData);
            }
            catch (Exception)
            {
                throw;
            }
        }

        protected bool IsEnabled(EventLevel level, EventKeywords keywords)
        {
            return true;
        }

        private string GetSource()
        {
            var eventSource = (EventSourceAttribute)this.GetType().GetCustomAttributes(typeof(EventSourceAttribute), false).Single();
            return eventSource.Name;
        }

        // Todo: Use a cache dictinary with eventId as key?
        private static EventAttribute GetEventAttribute(MethodBase eventMethod)
        {
            return (EventAttribute)eventMethod.GetCustomAttributes(typeof(EventAttribute), false).Single();
        }
    }
}
#endif

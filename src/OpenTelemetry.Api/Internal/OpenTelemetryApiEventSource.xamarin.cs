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

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// EventSource implementation for OpenTelemetry API.
    /// This is used for internal logging of this library.
    /// </summary>
    internal class OpenTelemetryApiEventSource : SelfLogBase
    {
        protected override string Source => "OpenTelemetry-Api";

        public static OpenTelemetryApiEventSource Log = new OpenTelemetryApiEventSource();

        // [NonEvent]
        public void ActivityContextExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.FailedToExtractActivityContext(format, ex.ToInvariantString());
            }
        }

        // [NonEvent]
        public void BaggageExtractException(string format, Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.FailedToExtractBaggage(format, ex.ToInvariantString());
            }
        }

        // [NonEvent]
        public void TracestateExtractException(Exception ex)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateExtractError(ex.ToInvariantString());
            }
        }

        // [NonEvent]
        public void TracestateKeyIsInvalid(ReadOnlySpan<char> key)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateKeyIsInvalid(key.ToString());
            }
        }

        // [NonEvent]
        public void TracestateValueIsInvalid(ReadOnlySpan<char> value)
        {
            if (this.IsEnabled(EventLevel.Warning, EventKeywords.All))
            {
                this.TracestateValueIsInvalid(value.ToString());
            }
        }

        // [Event(3, Message = "Failed to parse tracestate: too many items", Level = EventLevel.Warning)]
        public void TooManyItemsInTracestate()
        {
            this.WriteEvent(EventLevel.Warning, 3, "Failed to parse tracestate: too many items");
        }

        // [Event(4, Message = "Tracestate key is invalid, key = '{0}'", Level = EventLevel.Warning)]
        public void TracestateKeyIsInvalid(string key)
        {
            this.WriteEvent(EventLevel.Warning, 4, "Tracestate key is invalid, key = '{0}'", key);
        }

        // [Event(5, Message = "Tracestate value is invalid, value = '{0}'", Level = EventLevel.Warning)]
        public void TracestateValueIsInvalid(string value)
        {
            this.WriteEvent(EventLevel.Warning, 5, "Tracestate value is invalid, value = '{0}'", value);
        }

        // [Event(6, Message = "Tracestate parse error: '{0}'", Level = EventLevel.Warning)]
        public void TracestateExtractError(string error)
        {
            this.WriteEvent(EventLevel.Warning, 6, "Tracestate parse error: '{0}'", error);
        }

        // [Event(7, Message = "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", Level = EventLevel.Warning)]
        public void InvalidArgument(string methodName, string argumentName, string issue)
        {
            this.WriteEvent(EventLevel.Warning, 7, "Calling method '{0}' with invalid argument '{1}', issue '{2}'.", methodName, argumentName, issue);
        }

        // [Event(8, Message = "Failed to extract activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToExtractActivityContext(string format, string exception)
        {
            this.WriteEvent(EventLevel.Warning, 8, "Failed to extract activity context in format: '{0}', context: '{1}'.", format, exception);
        }

        // [Event(9, Message = "Failed to inject activity context in format: '{0}', context: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectActivityContext(string format, string error)
        {
            this.WriteEvent(EventLevel.Warning, 9, "Failed to inject activity context in format: '{0}', context: '{1}'.", format, error);
        }

        // [Event(10, Message = "Failed to extract baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToExtractBaggage(string format, string exception)
        {
            this.WriteEvent(EventLevel.Warning, 10, "Failed to extract baggage in format: '{0}', baggage: '{1}'.", format, exception);
        }

        // [Event(11, Message = "Failed to inject baggage in format: '{0}', baggage: '{1}'.", Level = EventLevel.Warning)]
        public void FailedToInjectBaggage(string format, string error)
        {
            this.WriteEvent(EventLevel.Warning, 11, "Failed to inject baggage in format: '{0}', baggage: '{1}'.", format, error);
        }
    }
}
#endif

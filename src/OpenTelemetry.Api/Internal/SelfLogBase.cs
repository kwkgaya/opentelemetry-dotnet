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
    public class SelfLogBase
    {
        public static Action<EventLevel, string> Listener;

        protected virtual string Source { get; }

        protected void WriteEvent(EventLevel level, int eventId, string message, params object[] args)
        {
            var log = $"Source: {Source}\n EventId: {eventId}\n {string.Format(message, args)}";
            Listener?.Invoke(level, log);
        }

        protected bool IsEnabled(EventLevel warning, EventKeywords all)
        {
            return true;
        }
    }
}
#endif

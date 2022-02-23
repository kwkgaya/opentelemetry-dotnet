// <copyright file="OtlpTraceExporterTests.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.ExportClient;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;
using OtlpCollector = Opentelemetry.Proto.Collector.Trace.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpTrace = Opentelemetry.Proto.Trace.V1;
using Status = OpenTelemetry.Trace.Status;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests
{
    public class OtlpTraceExporterTests : Http2UnencryptedSupportTests
    {
        static OtlpTraceExporterTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void OtlpExporter_BadArgs()
        {
            TracerProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddOtlpExporter());
        }

        [Fact]
        public void UserHttpFactoryCalled()
        {
            OtlpExporterOptions options = new OtlpExporterOptions();

            var defaultFactory = options.HttpClientFactory;

            int invocations = 0;
            options.Protocol = OtlpExportProtocol.HttpProtobuf;
            options.HttpClientFactory = () =>
            {
                invocations++;
                return defaultFactory();
            };

            using (var exporter = new OtlpTraceExporter(options))
            {
                Assert.Equal(1, invocations);
            }

            using (var provider = Sdk.CreateTracerProviderBuilder()
                .AddOtlpExporter(o =>
                {
                    o.Protocol = OtlpExportProtocol.HttpProtobuf;
                    o.HttpClientFactory = options.HttpClientFactory;
                })
                .Build())
            {
                Assert.Equal(2, invocations);
            }

            options.HttpClientFactory = null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new OtlpTraceExporter(options);
            });

            options.HttpClientFactory = () => null;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var exporter = new OtlpTraceExporter(options);
            });
        }

        [Fact]
        public void ServiceProviderHttpClientFactoryInvoked()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddHttpClient();

            int invocations = 0;

            services.AddHttpClient("OtlpTraceExporter", configureClient: (client) => invocations++);

            services.AddOpenTelemetryTracing(builder => builder.AddOtlpExporter(
                o => o.Protocol = OtlpExportProtocol.HttpProtobuf));

            using var serviceProvider = services.BuildServiceProvider();

            var tracerProvider = serviceProvider.GetRequiredService<TracerProvider>();

            Assert.Equal(1, invocations);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void ToOtlpResourceSpansTest(bool includeServiceNameInResource)
        {
            var evenTags = new[] { new KeyValuePair<string, object>("k0", "v0") };
            var oddTags = new[] { new KeyValuePair<string, object>("k1", "v1") };
            var sources = new[]
            {
                new ActivitySource("even", "2.4.6"),
                new ActivitySource("odd", "1.3.5"),
            };

            var resourceBuilder = ResourceBuilder.CreateEmpty();
            if (includeServiceNameInResource)
            {
                resourceBuilder.AddAttributes(
                    new List<KeyValuePair<string, object>>
                    {
                        new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceName, "service-name"),
                        new KeyValuePair<string, object>(ResourceSemanticConventions.AttributeServiceNamespace, "ns1"),
                    });
            }

            var builder = Sdk.CreateTracerProviderBuilder()
                .SetResourceBuilder(resourceBuilder)
                .AddSource(sources[0].Name)
                .AddSource(sources[1].Name);

            using var openTelemetrySdk = builder.Build();

            var exportedItems = new List<Activity>();
            var processor = new BatchActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
            const int numOfSpans = 10;
            bool isEven;
            for (var i = 0; i < numOfSpans; i++)
            {
                isEven = i % 2 == 0;
                var source = sources[i % 2];
                var activityKind = isEven ? ActivityKind.Client : ActivityKind.Server;
                var activityTags = isEven ? evenTags : oddTags;

                using Activity activity = source.StartActivity($"span-{i}", activityKind, parentContext: default, activityTags);
                processor.OnEnd(activity);
            }

            processor.Shutdown();

            var batch = new Batch<Activity>(exportedItems.ToArray(), exportedItems.Count);
            RunTest(batch);

            void RunTest(Batch<Activity> batch)
            {
                var request = new OtlpCollector.ExportTraceServiceRequest();

                request.AddBatch(resourceBuilder.Build().ToOtlpResource(), batch);

                Assert.Single(request.ResourceSpans);
                var oltpResource = request.ResourceSpans.First().Resource;
                if (includeServiceNameInResource)
                {
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.StringValue == "service-name");
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceNamespace && kvp.Value.StringValue == "ns1");
                }
                else
                {
                    Assert.Contains(oltpResource.Attributes, (kvp) => kvp.Key == ResourceSemanticConventions.AttributeServiceName && kvp.Value.ToString().Contains("unknown_service:"));
                }

                foreach (var instrumentationLibrarySpans in request.ResourceSpans.First().InstrumentationLibrarySpans)
                {
                    Assert.Equal(numOfSpans / 2, instrumentationLibrarySpans.Spans.Count);
                    Assert.NotNull(instrumentationLibrarySpans.InstrumentationLibrary);

                    var expectedSpanNames = new List<string>();
                    var start = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even" ? 0 : 1;
                    for (var i = start; i < numOfSpans; i += 2)
                    {
                        expectedSpanNames.Add($"span-{i}");
                    }

                    var otlpSpans = instrumentationLibrarySpans.Spans;
                    Assert.Equal(expectedSpanNames.Count, otlpSpans.Count);

                    var kv0 = new OtlpCommon.KeyValue { Key = "k0", Value = new OtlpCommon.AnyValue { StringValue = "v0" } };
                    var kv1 = new OtlpCommon.KeyValue { Key = "k1", Value = new OtlpCommon.AnyValue { StringValue = "v1" } };

                    var expectedTag = instrumentationLibrarySpans.InstrumentationLibrary.Name == "even"
                        ? kv0
                        : kv1;

                    foreach (var otlpSpan in otlpSpans)
                    {
                        Assert.Contains(otlpSpan.Name, expectedSpanNames);
                        Assert.Contains(expectedTag, otlpSpan.Attributes);
                    }
                }
            }
        }

        [Fact]
        public void ToOtlpSpanTest()
        {
            using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Producer);

            var attributes = new List<KeyValuePair<string, object>>
            {
                new KeyValuePair<string, object>("bool", true),
                new KeyValuePair<string, object>("long", 1L),
                new KeyValuePair<string, object>("string", "text"),
                new KeyValuePair<string, object>("double", 3.14),
                new KeyValuePair<string, object>("int", 1),
                new KeyValuePair<string, object>("datetime", DateTime.UtcNow),
                new KeyValuePair<string, object>("bool_array", new bool[] { true, false }),
                new KeyValuePair<string, object>("int_array", new int[] { 1, 2 }),
                new KeyValuePair<string, object>("double_array", new double[] { 1.0, 2.09 }),
                new KeyValuePair<string, object>("string_array", new string[] { "a", "b" }),
            };

            foreach (var kvp in attributes)
            {
                rootActivity.SetTag(kvp.Key, kvp.Value);
            }

            var startTime = new DateTime(2020, 02, 20, 20, 20, 20, DateTimeKind.Utc);

            DateTimeOffset dateTimeOffset;
            dateTimeOffset = DateTimeOffset.FromUnixTimeMilliseconds(0);

            var expectedUnixTimeTicks = (ulong)(startTime.Ticks - dateTimeOffset.Ticks);
            var duration = TimeSpan.FromMilliseconds(1555);

            rootActivity.SetStartTime(startTime);
            rootActivity.SetEndTime(startTime + duration);

            Span<byte> traceIdSpan = stackalloc byte[16];
            rootActivity.TraceId.CopyTo(traceIdSpan);
            var traceId = traceIdSpan.ToArray();

            var otlpSpan = rootActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("root", otlpSpan.Name);
            Assert.Equal(OtlpTrace.Span.Types.SpanKind.Producer, otlpSpan.Kind);
            Assert.Equal(traceId, otlpSpan.TraceId);
            Assert.Empty(otlpSpan.ParentSpanId);
            Assert.Null(otlpSpan.Status);
            Assert.Empty(otlpSpan.Events);
            Assert.Empty(otlpSpan.Links);
            OtlpTestHelpers.AssertOtlpAttributes(attributes, otlpSpan.Attributes);

            var expectedStartTimeUnixNano = 100 * expectedUnixTimeTicks;
            Assert.Equal(expectedStartTimeUnixNano, otlpSpan.StartTimeUnixNano);
            var expectedEndTimeUnixNano = expectedStartTimeUnixNano + (duration.TotalMilliseconds * 1_000_000);
            Assert.Equal(expectedEndTimeUnixNano, otlpSpan.EndTimeUnixNano);

            var childLinks = new List<ActivityLink> { new ActivityLink(rootActivity.Context, new ActivityTagsCollection(attributes)) };
            var childActivity = activitySource.StartActivity(
                "child",
                ActivityKind.Client,
                rootActivity.Context,
                links: childLinks);

            childActivity.SetStatus(Status.Error);

            var childEvents = new List<ActivityEvent> { new ActivityEvent("e0"), new ActivityEvent("e1", default, new ActivityTagsCollection(attributes)) };
            childActivity.AddEvent(childEvents[0]);
            childActivity.AddEvent(childEvents[1]);

            Span<byte> parentIdSpan = stackalloc byte[8];
            rootActivity.Context.SpanId.CopyTo(parentIdSpan);
            var parentId = parentIdSpan.ToArray();

            otlpSpan = childActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);
            Assert.Equal("child", otlpSpan.Name);
            Assert.Equal(OtlpTrace.Span.Types.SpanKind.Client, otlpSpan.Kind);
            Assert.Equal(traceId, otlpSpan.TraceId);
            Assert.Equal(parentId, otlpSpan.ParentSpanId);

            // Assert.Equal(OtlpTrace.Status.Types.StatusCode.NotFound, otlpSpan.Status.Code);

            Assert.Equal(Status.Error.Description ?? string.Empty, otlpSpan.Status.Message);
            Assert.Empty(otlpSpan.Attributes);

            Assert.Equal(childEvents.Count, otlpSpan.Events.Count);
            for (var i = 0; i < childEvents.Count; i++)
            {
                Assert.Equal(childEvents[i].Name, otlpSpan.Events[i].Name);
                OtlpTestHelpers.AssertOtlpAttributes(childEvents[i].Tags.ToList(), otlpSpan.Events[i].Attributes);
            }

            childLinks.Reverse();
            Assert.Equal(childLinks.Count, otlpSpan.Links.Count);
            for (var i = 0; i < childLinks.Count; i++)
            {
                OtlpTestHelpers.AssertOtlpAttributes(childLinks[i].Tags.ToList(), otlpSpan.Links[i].Attributes);
            }
        }

        [Fact]
        public void ToOtlpSpanActivitiesWithNullArrayTest()
        {
            using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Client);
            Assert.NotNull(rootActivity);

            var stringArr = new string[] { "test", string.Empty, null };
            rootActivity.SetTag("stringArray", stringArr);

            var otlpSpan = rootActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);

            var stringArray = otlpSpan.Attributes.Where(kvp => kvp.Key == "stringArray").ToList();

            Assert.NotNull(stringArray);
            Assert.Equal(3, stringArray.Count());
            Assert.Equal("test", stringArray[0].Value.StringValue);
            Assert.Equal(string.Empty, stringArray[1].Value.StringValue);
            Assert.Null(stringArray[2].Value);
        }

        [Fact]
        public void ToOtlpSpanPeerServiceTest()
        {
            using var activitySource = new ActivitySource(nameof(this.ToOtlpSpanTest));

            using var rootActivity = activitySource.StartActivity("root", ActivityKind.Client);

            rootActivity.SetTag(SemanticConventions.AttributeHttpHost, "opentelemetry.io");

            var otlpSpan = rootActivity.ToOtlpSpan();

            Assert.NotNull(otlpSpan);

            var peerService = otlpSpan.Attributes.FirstOrDefault(kvp => kvp.Key == SemanticConventions.AttributePeerService);

            Assert.NotNull(peerService);
            Assert.Equal("opentelemetry.io", peerService.Value.StringValue);
        }

        [Fact]
        public void UseOpenTelemetryProtocolActivityExporterWithCustomActivityProcessor()
        {
            if (Environment.Version.Major == 3)
            {
                // Adding the OtlpExporter creates a GrpcChannel.
                // This switch must be set before creating a GrpcChannel when calling an insecure HTTP/2 endpoint.
                // See: https://docs.microsoft.com/aspnet/core/grpc/troubleshoot#call-insecure-grpc-services-with-net-core-client
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            }

            const string ActivitySourceName = "otlp.test";
            TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                            .AddSource(ActivitySourceName)
                            .AddProcessor(testActivityProcessor)
                            .AddOtlpExporter()
                            .Build();

            using var source = new ActivitySource(ActivitySourceName);
            var activity = source.StartActivity("Test Otlp Activity");
            activity?.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);
        }

        [Fact]
        public void Shutdown_ClientShutdownIsCalled()
        {
            var exportClientMock = new Mock<IExportClient<OtlpCollector.ExportTraceServiceRequest>>();

            var exporter = new OtlpTraceExporter(new OtlpExporterOptions(), exportClientMock.Object);

            var result = exporter.Shutdown();

            exportClientMock.Verify(m => m.Shutdown(It.IsAny<int>()), Times.Once());
        }
    }
}

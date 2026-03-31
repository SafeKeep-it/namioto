# Thoth.Tests - PROJECT

## Purpose

- This project is a focused xUnit test suite for `Thoth.Eventing` behavior, validating dispatch/raising semantics and event ordering through dedicated fixture classes and custom test widgets, using .NET 10 and Thoth project references. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:4-20; src/dotnet/Thoth.Tests/eventing/raised/event_raised_is_enqueued.cs:11-47; src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs:10-23

## Key Directories

- eventing/dispatch: tests for event path computation (`GetCapturePath`, `GetBubblePath`) and handler/observer skipping when events are marked handled. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_capture_path_for_event_type.cs:27-47; src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_bubble_path_for_event_type.cs:30-55; src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/capture_is_skipped_when_handled.cs:23-37; src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/bubble_is_skipped_when_handled.cs:23-37
- eventing/raised: tests for dispatch queue behavior, event enqueuing, and ordering between input and raised events. Evidence: src/dotnet/Thoth.Tests/eventing/raised/event_raised_is_enqueued.cs:16-47; src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs:11-23
- eventing/raised/utilities and eventing/dispatch/utilities: helper test widgets and observers used to build widget trees and assert routing behavior. Evidence: src/dotnet/Thoth.Tests/eventing/raised/utilities/OrderTrackingWidget.cs:8-28; src/dotnet/Thoth.Tests/eventing/dispatch/utilities/CapturingWidget.cs:7-18; src/dotnet/Thoth.Tests/eventing/dispatch/utilities/HandlingWidget.cs:7-18; src/dotnet/Thoth.Tests/eventing/raised/utilities/EventRaisingWidget.cs:8-22

## Primary Entry Points

- src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_capture_path_for_event_type.cs: validates capture-path resolution and exclusion rules for non-capturing widgets. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_capture_path_for_event_type.cs:27-47
- src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_bubble_path_for_event_type.cs: validates bubble-path resolution and order from target to root. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_bubble_path_for_event_type.cs:30-55
- src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/capture_is_skipped_when_handled.cs: checks capture handling stops once marked handled. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/capture_is_skipped_when_handled.cs:17-36
- src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/bubble_is_skipped_when_handled.cs: checks bubble handling stops once marked handled. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/bubble_is_skipped_when_handled.cs:20-36
- src/dotnet/Thoth.Tests/eventing/raised/event_raised_is_enqueued.cs: verifies queueing and dispatching raises events in handler order. Evidence: src/dotnet/Thoth.Tests/eventing/raised/event_raised_is_enqueued.cs:16-48
- src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs: asserts initial input events are processed before raised follow-up events. Evidence: src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs:11-23

## Public API Surface

- Thoth.Tests.eventing.dispatch.event_not_handled.builds_capture_path_for_event_type: public fixture with xUnit facts for capture-path assertions. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_capture_path_for_event_type.cs:9-31
- Thoth.Tests.eventing.dispatch.event_not_handled.builds_bubble_path_for_event_type: public fixture with xUnit facts for bubble-path assertions. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_bubble_path_for_event_type.cs:9-31
- Thoth.Tests.eventing.dispatch.event_handled.capture_is_skipped_when_handled: public fixture with xUnit facts for handled capture behavior. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/capture_is_skipped_when_handled.cs:9-37
- Thoth.Tests.eventing.dispatch.event_handled.bubble_is_skipped_when_handled: public fixture with xUnit facts for handled bubble behavior. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/bubble_is_skipped_when_handled.cs:9-37
- Thoth.Tests.eventing.raised.event_raised_is_enqueued: public fixture exercising `EventDispatcher.Dispatch` and `DispatchAll`. Evidence: src/dotnet/Thoth.Tests/eventing/raised/event_raised_is_enqueued.cs:8-47
- Thoth.Tests.eventing.raised.order_of_events: public fixture for event ordering expectations across raised events. Evidence: src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs:8-23

## Constraints (if proven)

- Test target is `net10.0`, and the project is not packable, which affects how this project is consumed. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:4,7
- Test dependencies are fixed to xunit + Shouldly + Microsoft.NET.Test.Sdk + coverlet.collector versions declared in the project file. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:11-15
- Tests reference the Thoth library via `ProjectReference`, with one reference using a non-standard path string (`dotet`) and one using the conventional relative path. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:19-20

## Common Tasks

- Add/adjust event dispatch behavior tests by creating or editing fixture classes in `eventing/dispatch` and `eventing/raised`. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_capture_path_for_event_type.cs:9-47; src/dotnet/Thoth.Tests/eventing/raised/order_of_events.cs:8-23
- Add or update custom widgets and observers in the utilities folders to model event capture/handle/observe scenarios for assertions. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/utilities/CapturingWidget.cs:7-18; src/dotnet/Thoth.Tests/eventing/dispatch/utilities/ObserveCapturedWidget.cs:7-13; src/dotnet/Thoth.Tests/eventing/raised/utilities/EventRaisingWidget.cs:8-22
- Use `IAsyncLifetime` for fixtures that need async setup/teardown around dispatcher path/handling scenarios. Evidence: src/dotnet/Thoth.Tests/eventing/dispatch/event_not_handled/builds_bubble_path_for_event_type.cs:9-34; src/dotnet/Thoth.Tests/eventing/dispatch/event_handled/bubble_is_skipped_when_handled.cs:9-27
- Update `Thoth.Tests.csproj` when new package dependencies or project links are required. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:10-21

## Notes

- The project has two `ProjectReference` entries to Thoth, including a `dotet` variant path and one conventional `..\\Thoth\\Thoth.csproj` entry. Evidence: src/dotnet/Thoth.Tests/Thoth.Tests.csproj:19-20

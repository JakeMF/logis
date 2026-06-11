# Test Script: T3_MultiFileFeature_CS

## Overview
This is a Tier 3 test spanning **three interdependent files**. It simulates a decoupled C# system using interfaces and dependency injection. It is designed to test the agent's ability to discover dependencies, navigate multiple files, and maintain structural integrity across a distributed change.

## Project Structure
- `IAnalyticsService.cs`: The interface contract.
- `CloudAnalyticsService.cs`: The implementation logic.
- `Program.cs`: The consumer of the service.

## Improvement Checklist
The model must successfully complete the following multi-file operation:
- [ ] **Interface Update:** Add `void TrackEvent(string name, IDictionary<string, string> properties);` to `IAnalyticsService.cs`.
- [ ] **Implementation Update:** Implement the `TrackEvent` method in `CloudAnalyticsService.cs` (e.g., printing the event name and all dictionary properties to the console).
- [ ] **Consumer Update:** In `Program.cs`, call the new `TrackEvent` method after the page views, passing a 'PurchaseCompleted' event with at least two properties (e.g., "Amount" and "Currency").
- [ ] **Type Integrity:** Ensure that the dictionary type used matches exactly across all three files (e.g., `IDictionary<string, string>`).
- [ ] **Documentation:** Update XML summaries for the new method in both the interface and the service.

## Evaluation Prompts

### Vague Prompt
> "I need to track more than just page views. Can you add support for custom event tracking with properties to our analytics system and then track a purchase in the main program?"

```bash
logis --file test-workspaces/tier-3/T3_MultiFileFeature_CS --task "I need to track more than just page views. Can you add support for custom event tracking with properties to our analytics system and then track a purchase in the main program?"
```

### Direct Prompt
> "Add a new method called 'TrackEvent' to the IAnalyticsService interface that takes a string name and an IDictionary<string, string> of properties. Implement this method in CloudAnalyticsService to print the event data to the console. Finally, update Program.cs to track a 'PurchaseCompleted' event with some sample price data after the checkout page view."

```bash
logis --file test-workspaces/tier-3/T3_MultiFileFeature_CS --task "Add a new method called 'TrackEvent' to the IAnalyticsService interface that takes a string name and an IDictionary<string, string> of properties. Implement this method in CloudAnalyticsService to print the event data to the console. Finally, update Program.cs to track a 'PurchaseCompleted' event with some sample price data after the checkout page view."
```

## Expected Outcome
The resulting project should compile and run correctly. Success is defined by:
1.  **Consistent Method Signature:** The exact same method signature exists in the interface and the implementation.
2.  **No Missing Links:** The `Program.cs` file successfully calls the new method.
3.  **Correct Scoping:** The agent correctly identifies that the change requires touching all three files to maintain the contract.

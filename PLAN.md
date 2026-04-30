# Syntax Highlighting Re-implementation Plan

## Problem Analysis
The current syntax highlighting implementation in `IncrementalHighlighter.cs` and related files has a solid foundation but can still hang the system under certain conditions due to:
1. Regex-based tokenization that can suffer from catastrophic backtracking
2. Lack of explicit timeouts for parsing operations
3. Potential inefficiencies in the worker loop under rapid typing scenarios
4. No frame rate capping mechanism to ensure UI responsiveness

## Solution Overview
We will enhance the existing incremental highlighter with:
1. Improved timeout mechanisms for parsing operations
2. Frame rate capping to maintain 60fps UI responsiveness
3. Enhanced state machine with iteration limits to prevent infinite loops
4. Better batching and yielding in the worker loop
5. Fallback to plain text on repeated failures

## Specific Changes

### 1. IncrementalHighlighter.cs Improvements

#### Add Configuration Constants
```csharp
// Add these constants at class level
private const int MaxParseTimeMs = 16; // ~60fps (16ms per frame)
private const int MaxIterationsPerLine = 10000; // Prevent infinite loops
private const int MaxWorkerBatchTimeMs = 50; // Max time worker spends per batch
```

#### Enhance WorkerLoopAsync with Time-Based Yielding
Replace the current WorkerLoopAsync method with a version that:
- Tracks time spent processing
- Yields control periodically to maintain responsiveness
- Implements frame-aware processing

#### Add Timeout Protection to TokenizeLine
Modify TokenizeLine method to:
- Track iterations and abort if exceeding MaxIterationsPerLine
- Track elapsed time and abort if exceeding MaxParseTimeMs
- Return partially processed tokens or fall back to base state on timeout

#### Improve State Management with Cycle Detection
Add detection for:
- Repeated state patterns that indicate infinite loops
- Invalid state transitions
- State corruption recovery

### 2. Form1.cs Improvements

#### Enhance Debouncing Mechanism
- Keep the 150ms debounce but make it adaptive based on system load
- Add immediate highlighting for small changes (< 100ms since last highlight)
- Increase delay during rapid typing bursts

#### Add Frame Rate Monitoring
- Track actual time taken for highlighting operations
- Adjust worker priority based on frame timing
- Implement dynamic quality reduction if frames are missed

### 3. Error Handling and Fallback Improvements

#### Enhance ApplyHighlightPatch
- Add retry mechanism with exponential backoff
- Implement circuit breaker pattern for repeatedly failing lines
- Graceful degradation to plain text with visual indication of issues

#### Add Telemetry for Performance Monitoring
- Track highlighting latency
- Count timeouts and fallback occurrences
- Measure cache hit/miss ratios

## Implementation Approach

We will maintain the existing architecture but enhance it with:
1. Better time-bound processing in the worker thread
2. More robust state machine with safety limits
3. Improved error recovery mechanisms
4. Adaptive performance tuning

## Files to Modify
1. `IncrementalHighlighter.cs` - Core parsing logic improvements
2. `Form1.cs` - UI integration and timing enhancements

## Testing Strategy
1. Unit tests for timeout scenarios
2. Stress tests with large files and rapid typing
3. Fuzz testing with malformed inputs
4. Performance benchmarks before/after changes
5. Manual testing with known problematic syntax patterns

## Expected Outcomes
- Elimination of system hangs during syntax highlighting
- Consistent 60fps UI responsiveness during editing
- Graceful degradation to plain text on parsing errors
- Maintained or improved highlighting accuracy
- Better handling of edge cases and malformed input
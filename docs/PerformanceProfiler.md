# Performance Profiler for Pfpad

## Overview
Pfpad includes a built-in performance profiler similar to Chrome DevTools, providing session recording, flame charts, memory snapshots, and allocation tracking to identify performance bottlenecks and memory issues.

## Features

### Session Recording and Flame Charts
- **Recording**: Start/stop recording to capture performance events during file operations, UI updates, and other activities.
- **Flame Chart**: Displays long-running tasks (>50ms) with duration, start time, and category.
- **Call Tree**: Hierarchical view of function calls for each long task.
- **Automatic Detection**: Any operation wrapped with `RecordEvent` is monitored for slow execution.

### Performance Analysis
- **Call Tree View**: Bottom-up and top-down views of hot code paths.
- **Summary Statistics**: Total recorded time, number of long tasks, average durations.
- **Timeline Integration**: Events are timestamped for correlation with user actions.

### Memory Profiling
- **Heap Snapshots**: Capture memory state before/after operations to detect leaks.
- **Snapshot Comparison**: Compare two snapshots to identify retained objects and memory growth.
- **GC Monitoring**: Real-time monitoring of garbage collection activity indicating memory pressure.

### Allocation Instrumentation
- **Memory Tracking**: Snapshots include total memory, GC memory, and process working set.
- **Leak Detection**: Compare snapshots during file loads to spot memory spikes.

### Logging and Tracing
- **Console Logs**: Timestamped logs for all profiler activities and file operations.
- **File I/O Logging**: Detailed logging around file open/read operations with timestamps.
- **Error Handling**: Try/catch blocks prevent crashes and log exceptions.

### Native Profiler Integration
- **CPU Stacks**: Records call stacks for performance events using `StackTrace`.
- **Main Thread Monitoring**: Tracks UI thread blocking operations.
- **Async Offloading**: Heavy synchronous work (like file parsing) is moved to background threads.

## Usage

### Opening the Profiler
- Go to Help > Performance Profiler
- The dialog has three tabs: Performance, Memory, Console

### Recording Performance
1. Click "Start Recording" to begin capturing events.
2. Perform operations (e.g., open files, edit code).
3. Click "Stop Recording" to analyze results.
4. View flame chart for long tasks, call tree for stack traces.

### Memory Analysis
1. Click "Take Snapshot" before/after operations.
2. Use "Compare Last Two" to analyze memory differences.
3. Monitor console for GC activity logs.

### Console Logging
- View real-time logs of profiler activities and system events.
- Logs include timestamps and detailed operation information.

## Integration Points

### File Operations
- `OpenFileInNewTab` and `OpenFileInNewTabAsync` include logging and performance recording.
- File size limits prevent loading huge files that could cause memory issues.
- Async loading prevents UI blocking.

### Event Recording
- Use `RecordEvent(name, category, action)` to wrap operations for monitoring.
- Automatically captures call stacks and durations for slow tasks.

### GC Monitoring
- Background timer checks for GC collections every second.
- Logs activity when collections occur, indicating memory pressure.

## Implementation Details

### PerformanceProfilerDialog Class
- **Location**: `apps/MyCrownJewelApp/src/MyCrownJewelApp.Pfpad/PerformanceProfilerDialog.cs`
- **Tabs**: Performance (recording/events), Memory (snapshots), Console (logs)
- **Data Structures**:
  - `PerformanceEvent`: Records name, duration, category, call stack
  - `MemorySnapshot`: Captures memory stats and details

### Form1 Integration
- **Menu**: Help > Performance Profiler
- **Logging**: All file operations log to profiler console
- **Recording**: File reads are automatically recorded
- **GC Timer**: Monitors garbage collection in background

### Asynchronous Operations
- File loading uses `Task.Run` for I/O operations
- UI updates use `BeginInvoke` to avoid thread issues
- Heavy parsing moved to worker threads

## Troubleshooting Performance Issues

### Identifying Long Tasks
1. Start recording
2. Perform the slow operation
3. Stop recording
4. Check flame chart for tasks >50ms
5. Examine call tree for blocking functions

### Detecting Memory Leaks
1. Take snapshot before operation
2. Perform operation
3. Take snapshot after
4. Compare to see memory growth
5. Watch console for frequent GC logs

### Reducing UI Blocking
- Ensure heavy work uses async patterns
- Split large operations into chunks with `Task.Delay`
- Use `BackgroundWorker` or `Task.Run` for CPU-intensive tasks

## Limitations
- Flame chart is simplified (list view) compared to Chrome's graphical chart
- Memory snapshots are basic (GC stats) without detailed heap analysis
- Allocation tracking is event-based, not instrumented per allocation
- Native profiling limited to managed code stacks

## Future Enhancements
- Graphical flame chart with SVG rendering
- Detailed heap analysis with object graphs
- Allocation sampling with ETW events
- Cross-process tracing with Event Tracing for Windows (ETW)
- Performance marks and measures like browser APIs
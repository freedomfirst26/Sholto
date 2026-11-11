// Test classes here build Avalonia controls with no Avalonia application running, on whatever xUnit thread
// the class lands on. Avalonia's AvaloniaPropertyDictionaryPool is a process-wide static Stack that is not
// thread-safe, so parallel classes intermittently fail with "InvalidOperationException: Stack empty" in
// ValueStore.SetInheritanceParent. The whole suite runs in about 0.3 s, so run its classes one at a time.
// Same family as MainUI's AvaloniaXamlCollection.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

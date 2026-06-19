using Xunit;

[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace MyCrownJewelApp.Tests;

[CollectionDefinition("Sequential")]
public sealed class SequentialCollectionDefinition
{
}

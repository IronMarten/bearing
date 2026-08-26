using TestBed.Core.Areas.HelpPage;

namespace TestBed.Core;

// Plant for D63. This references a type the default path exclusions remove, so the walk records
// an outbound reference that the edge list cannot carry: the symbol is in the solution and no node
// was ever built for it.
//
// That asymmetry is what the defect was. Outbound was added during the walk, which cannot yet know
// whether the target got a node; inbound is added in ModelBuilder.Build, which can, and skipped
// what did not resolve. So FanOut counted this reference and FanIn did not, and a type's FanOut
// column disagreed with edges.csv -- on 1.0% of nopCommerce's types and 6.7% of Jellyfin's, and on
// none of this fixture's, which is why 538 tests could not see it.
//
// Nothing in TestBed reached an excluded type before this. NeighbourhoodTests
// .Reconciles_with_fan_in_and_fan_out fails without the prune in ModelBuilder.Build, and
// Coverage.EdgesToUnanalysedTypes moves off zero for the first time, which is the disclosure that
// says so in the report.
public sealed class UnanalysedEdgeTrap
{
    public object Sample(HelpPageSampleGenerator generator) => generator.GetSample("a", "b", 0);
}

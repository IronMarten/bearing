// PLANT: cross-project fully-qualified name collision, declaration 1 of 2.
//
// The other declaration is tests/TestBed/Tools/PayloadTag.cs — same namespace, same name,
// same `partial` keyword, different assembly. .NET permits this and plugin architectures use
// it deliberately; Data and Tools do not reference each other, so it compiles cleanly.
//
// `partial` is the point. Within one compilation, two partial declarations ARE one type and
// must keep merging. Across compilations they are two types, and merging them sums FanIn,
// FanOut, Cyclomatic, Dsm, Loc and MemberCount into a single row — which on nopCommerce
// fabricated a five-project circular reference. See TECHREQ-job-b.md §8 criterion 8.
//
// This declaration is deliberately small and simple. Tools' is large and branchy, so a merged
// row is obvious on inspection rather than a subtle numeric shift.

namespace TestBed.Shared;

public partial class PayloadTag
{
    public string Label { get; set; }

    public string Describe()
    {
        return Label ?? "untagged";
    }
}

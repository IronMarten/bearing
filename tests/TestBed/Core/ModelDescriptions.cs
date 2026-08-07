using System.Collections.ObjectModel;

namespace TestBed.Core;

// Reproduces a real false positive: a cohort of trivial property bags where the peer
// median complexity is 0, so ANY constructor with a single assignment scored as an
// infinite-times outlier and was reported as a concealed decision at cc 1.
public class ModelDescription
{
    public string Name { get; set; }
    public string Documentation { get; set; }
}

public class ParameterDescription
{
    public string Name { get; set; }
    public string TypeName { get; set; }
}

public class SimpleTypeModelDescription : ModelDescription { }
public class EnumTypeModelDescription : ModelDescription { }
public class KeyValuePairModelDescription : ModelDescription { }

public class CollectionModelDescription : ModelDescription
{
    public ModelDescription ElementDescription { get; set; }
}

public class ComplexTypeModelDescription : ModelDescription
{
    public ComplexTypeModelDescription()
    {
        Properties = new Collection<ParameterDescription>();
    }

    public Collection<ParameterDescription> Properties { get; private set; }
}

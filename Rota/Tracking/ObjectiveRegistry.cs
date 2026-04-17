using System.Collections.Generic;
using System.Linq;

namespace Rota.Tracking;

public sealed class ObjectiveRegistry
{
    private readonly List<IObjective> _objectives = new();

    public IReadOnlyList<IObjective> All => _objectives;

    public void Register(IObjective o) => _objectives.Add(o);

    public IEnumerable<IObjective> ByCadence(ObjectiveCadence c) =>
        _objectives.Where(o => o.Cadence == c);

    public IEnumerable<IObjective> ByCategory(string category) =>
        _objectives.Where(o => o.Category == category);
}

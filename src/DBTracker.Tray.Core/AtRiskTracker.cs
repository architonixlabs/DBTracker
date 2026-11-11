using System.Collections.Generic;
using System.Linq;

namespace DBTracker.Tray.Core
{
    // Remembers which instances were at risk on the previous poll so only newly at-risk ones notify.
    public sealed class AtRiskTracker
    {
        private HashSet<long> _previous;

        // The first summary is the baseline: nothing is "new" yet.
        public IReadOnlyList<AtRiskInstance> Update(TraySummary summary)
        {
            var current = summary?.AtRisk ?? new AtRiskInstance[0];
            var ids = new HashSet<long>(current.Select(i => i.Id));

            if (_previous == null)
            {
                _previous = ids;
                return new AtRiskInstance[0];
            }

            var added = current.Where(i => !_previous.Contains(i.Id)).ToList();
            _previous = ids;
            return added;
        }

        public void Reset() => _previous = null;
    }
}

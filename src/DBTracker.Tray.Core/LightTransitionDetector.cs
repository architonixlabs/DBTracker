namespace DBTracker.Tray.Core
{
    public sealed class LightTransition
    {
        public LightTransition(StatusLight from, StatusLight to)
        {
            From = from;
            To = to;
        }

        public StatusLight From { get; }
        public StatusLight To { get; }
    }

    // Reports a change only after the new light has been seen on consecutive polls, so a single
    // failed request (an app pool recycle, a slow first request) doesn't notify.
    public sealed class LightTransitionDetector
    {
        private readonly int _confirmations;
        private StatusLight _pending = StatusLight.Unknown;
        private int _pendingCount;

        public LightTransitionDetector(int confirmations = 2)
        {
            _confirmations = confirmations < 1 ? 1 : confirmations;
        }

        public StatusLight Confirmed { get; private set; } = StatusLight.Unknown;

        // The first confirmed light is the baseline and never reported.
        public LightTransition Observe(StatusLight light)
        {
            if (light == Confirmed)
            {
                _pending = StatusLight.Unknown;
                _pendingCount = 0;
                return null;
            }

            if (light == _pending)
            {
                _pendingCount++;
            }
            else
            {
                _pending = light;
                _pendingCount = 1;
            }

            var needed = Confirmed == StatusLight.Unknown ? 1 : _confirmations;
            if (_pendingCount < needed)
            {
                return null;
            }

            var previous = Confirmed;
            Confirmed = light;
            _pending = StatusLight.Unknown;
            _pendingCount = 0;
            return previous == StatusLight.Unknown ? null : new LightTransition(previous, light);
        }
    }
}

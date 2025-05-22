using Prometheus;

namespace TransactionService.Tests.TestHelpers
{
    public interface IMetricWrapper
    {
        void Inc();
        void Inc(double increment);
        void WithLabels(string label);
    }

    public class CounterWrapper : IMetricWrapper
    {
        private readonly Counter _counter;
        private Counter.Child _child;

        public CounterWrapper(Counter counter)
        {
            _counter = counter;
            _child = counter.WithLabels("");
        }

        public void Inc()
        {
            _child.Inc();
        }

        public void Inc(double increment)
        {
            _child.Inc(increment);
        }

        public void WithLabels(string label)
        {
            _child = _counter.WithLabels(label);
        }
    }

    public class HistogramWrapper
    {
        private readonly Histogram _histogram;
        
        public HistogramWrapper(Histogram histogram)
        {
            _histogram = histogram;
        }
        
        public void Observe(double value)
        {
            _histogram.Observe(value);
        }
    }
}
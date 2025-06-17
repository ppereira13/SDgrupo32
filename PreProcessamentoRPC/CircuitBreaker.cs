using System;
using System.Threading;
using System.Threading.Tasks;

namespace PreProcessamentoRPC
{
    public enum CircuitState
    {
        Closed,
        Open,
        HalfOpen
    }

    public class CircuitBreaker
    {
        private readonly object _stateLock = new object();
        private readonly int _failureThreshold;
        private readonly TimeSpan _resetTimeout;
        private CircuitState _currentState;
        private int _failureCount;
        private DateTime? _lastFailureTime;

        public CircuitBreaker(int failureThreshold = 3, int resetTimeoutSeconds = 60)
        {
            _failureThreshold = failureThreshold;
            _resetTimeout = TimeSpan.FromSeconds(resetTimeoutSeconds);
            _currentState = CircuitState.Closed;
            _failureCount = 0;
        }

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> action)
        {
            if (ShouldRethrowException())
            {
                throw new CircuitBreakerOpenException("Circuit breaker está aberto. Tentativas temporariamente bloqueadas.");
            }

            try
            {
                T result = await action();
                Reset();
                return result;
            }
            catch (Exception ex)
            {
                HandleFailure(ex);
                throw;
            }
        }

        private bool ShouldRethrowException()
        {
            lock (_stateLock)
            {
                if (_currentState == CircuitState.Open)
                {
                    if (_lastFailureTime.HasValue && DateTime.UtcNow - _lastFailureTime.Value >= _resetTimeout)
                    {
                        _currentState = CircuitState.HalfOpen;
                        return false;
                    }
                    return true;
                }
                return false;
            }
        }

        private void HandleFailure(Exception ex)
        {
            lock (_stateLock)
            {
                _failureCount++;
                _lastFailureTime = DateTime.UtcNow;

                if (_failureCount >= _failureThreshold || _currentState == CircuitState.HalfOpen)
                {
                    _currentState = CircuitState.Open;
                    Console.WriteLine($"Circuit breaker aberto após {_failureCount} falhas. Último erro: {ex.Message}");
                }
            }
        }

        private void Reset()
        {
            lock (_stateLock)
            {
                _currentState = CircuitState.Closed;
                _failureCount = 0;
                _lastFailureTime = null;
            }
        }

        public CircuitState State
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
    }
} 
using System;
using System.Reactive.Subjects;

/// <summary>
/// A type that holds a value that can be subscribed against. Also this wrapper implements
/// INotifyPropertyChanged against the wrapped value for data-binding.
/// </summary>
/// <typeparam name="T"></typeparam>
public class ReactiveProperty<T> : IObservable<T>, IDisposable, IComparable
{
    private T _value;
    private readonly Subject<T> _valueObservable = new Subject<T>();

    public ReactiveProperty(T initialValue = default(T))
    {
        _value = initialValue;
    }

    public T Value
    {
        get { return _value; }
        set
        {
            if (!Equals(_value, value))
            {
                _value = value;
                _valueObservable.OnNext(value);
            }
        }
    }

    public static implicit operator T(ReactiveProperty<T> reactiveProperty) => reactiveProperty.Value;

    public IDisposable Subscribe(IObserver<T> observer)
    {
        var subscription = _valueObservable.Subscribe(observer);
        return subscription;
    }

    public virtual void Dispose() => _valueObservable.Dispose();

    public override string ToString() => Value?.ToString() ?? string.Empty;

    public int CompareTo(object obj)
    {
        var comparable = _value as IComparable;
        if (comparable != null)
        {
            return comparable.CompareTo(obj);
        }
        throw new InvalidOperationException($"The underlying type {(typeof(T)).FullName} does not implement IComparable so a comparison is not possible.");
    }
}
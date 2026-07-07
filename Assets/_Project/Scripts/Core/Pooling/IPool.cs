/// <summary>
/// Minimal object-pool contract: take an instance out, give it back.
/// </summary>
public interface IPool<T>
{
    T Get();
    void Release(T item);
}

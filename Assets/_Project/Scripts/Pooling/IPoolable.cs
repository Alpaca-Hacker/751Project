namespace SoftBody.Scripts.Pooling
{
    public interface IPoolable
    {
        void Initialize(GenericObjectPool pool);
        void OnGetFromPool();
        void OnReturnToPool();
        void ReturnToPool();
    }
}
namespace APIFramework.Core;

public interface ISystem
{
    // Every update, the system gets the manager and the time elapsed
    void Update(EntityManager em, float deltaTime);
}
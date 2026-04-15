using UnityEngine;

public class Shoot : MonoBehaviour
{
    [SerializeField] GameObject shot;
    [SerializeField] ParticleSystem particle;

    public void ShootIt()
    {
        shot.SetActive(false);
        var random = UnityEngine.Random.Range(0f, .5f);
        Invoke(nameof(ActivateShot), random);
    }
    public void ActivateShot()
    {
        shot.SetActive(true);
        particle.Play();
    }
}

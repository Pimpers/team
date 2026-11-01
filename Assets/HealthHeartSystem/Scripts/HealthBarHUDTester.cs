/*
 *  Author: ariel oliveira [o.arielg@gmail.com]
 */

using UnityEngine;

// Wrapped in a namespace so this demo PlayerStats doesn't clash with the game's own
// global PlayerStats (Assets/CS/PlayerStats.cs). Prefabs/scenes reference scripts by GUID, so
// this asset's ExampleScene keeps working.
namespace HealthHeartSystem
{
public class HealthBarHUDTester : MonoBehaviour
{
    public void AddHealth()
    {
        PlayerStats.Instance.AddHealth();
    }

    public void Heal(float health)
    {
        PlayerStats.Instance.Heal(health);
    }

    public void Hurt(float dmg)
    {
        PlayerStats.Instance.TakeDamage(dmg);
    }
}
}

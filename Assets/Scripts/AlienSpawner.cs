using UnityEngine;

public class AlienSpawner : MonoBehaviour
{
    [SerializeField] private GameObject _alienPrefab;
    [SerializeField] private int _alienCount = 5;
    [SerializeField] private Transform _parent;
    void Start()
    {
        for (int i = 0; i < _alienCount; i++)
        {
            Vector3 spawnPosition = Universe.Instance.GetRandomPositionAwayFrom();
            Instantiate(_alienPrefab, spawnPosition, Quaternion.identity, _parent);
        }
    }

}

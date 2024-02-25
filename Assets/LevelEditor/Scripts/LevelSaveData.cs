using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LevelSaveData
{
    public LevelSaveData()
    {
        IDs = new List<int>();
        Positions = new List<Vector3>();
        Rotations = new List<Quaternion>();
    }

    public List<int> IDs;
    public List<Vector3> Positions;
    public List<Quaternion> Rotations;
}

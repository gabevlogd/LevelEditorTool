using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    /// <summary>
    /// Has a door on the north wall?
    /// </summary>
    [SerializeField] private bool _north;
    /// <summary>
    /// Has a door on the east wall?
    /// </summary>
    [SerializeField] private bool _east;
    /// <summary>
    /// Has a door on the south wall?
    /// </summary>
    [SerializeField] private bool _south;
    /// <summary>
    /// Has a door on the west wall?
    /// </summary>
    [SerializeField] private bool _west;

    /// <summary>
    /// If this room has a door on the north wall it return a direction pointing from 
    /// the center of the room to the north door otherwise return Vector3.Zero
    /// </summary>
    public Vector3 North
    {
        get
        {
            if (_north)
                return transform.forward;
            else return Vector3.zero;
        }
    }

    /// <summary>
    /// If this room has a door on the east wall it return a direction pointing from 
    /// the center of the room to the east door otherwise return Vector3.Zero
    /// </summary>
    public Vector3 East
    {
        get
        {
            if (_east)
                return transform.right;
            else return Vector3.zero;
        }
    }

    /// <summary>
    /// If this room has a door on the south wall it return a direction pointing from 
    /// the center of the room to the south door otherwise return Vector3.Zero
    /// </summary>
    public Vector3 South
    {
        get
        {
            if (_south)
                return -transform.forward;
            else return Vector3.zero;
        }
    }

    /// <summary>
    /// If this room has a door on the west wall it return a direction pointing from 
    /// the center of the room to the west door otherwise return Vector3.Zero
    /// </summary>
    public Vector3 West
    {
        get
        {
            if (_west)
                return -transform.right;
            else return Vector3.zero;
        }
    }
}

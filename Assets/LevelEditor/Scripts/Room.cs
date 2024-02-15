using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Room : MonoBehaviour
{
    [SerializeField]
    private bool _north;
    [SerializeField]
    private bool _east;
    [SerializeField]
    private bool _south;
    [SerializeField]
    private bool _west;


    public Vector3 North
    {
        get
        {
            if (_north)
                return transform.forward;
            else return Vector3.zero;
        }
    }

    public Vector3 East
    {
        get
        {
            if (_east)
                return transform.right;
            else return Vector3.zero;
        }
    }

    public Vector3 South
    {
        get
        {
            if (_south)
                return -transform.forward;
            else return Vector3.zero;
        }
    }

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

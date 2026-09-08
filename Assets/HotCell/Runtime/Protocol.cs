using System;
using UnityEngine;

namespace HotCell
{
    [Serializable]
    public sealed class InputFrame
    {
        public int sequence;
        public float horizontal, vertical, yaw, pitch;
        public bool use, reverse, lamp = true, wrist, clipboard;
        public int selectedSuit = 1;
    }

    // This is the entire public per-suit payload. Exact dose and role never belong here.
    [Serializable]
    public sealed class SuitFrame
    {
        public int slot;
        public Vector3 position;
        public float yaw;
        public bool connected, collapsed, ejected, lamp;
    }

    // A distinct snapshot is constructed for each recipient on the host.
    [Serializable]
    public sealed class ShiftFrame
    {
        public int tick, localSlot, phase;
        public SuitFrame[] suits;
        public Vector3 caskPosition, shieldPosition, hookPosition;
        public Quaternion caskRotation, shieldRotation;
        public bool doorOpen, ventilation, alarm, loaded;
        public float remaining, localDose, localRate;
        public bool localTraitor;
        public int gaugeBolt = -1;
        public float gaugeTorque;
        public int inspectedSuit;
        public float inspectedDose;
        public int lockoutTarget;
        public string report;
    }
}

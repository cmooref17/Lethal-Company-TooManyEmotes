using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TooManyEmotes.AdditionalData
{
    class AdditionalPropData
    {
        public string propParentPath;
        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        public Vector3 leftHandPositionIK = Vector3.zero;
        public Vector3 rightHandPositionIK = Vector3.zero;
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;


namespace Assets.Global.Scripts
{
    /// <summary>
    /// This class serves to describe ray that last undefined number of frames.
    /// </summary>
    class LastingDebugDay
    {
        private Vector3 Start;
        private Vector3 Direction;
        private Color Color;
        private bool show;

        public LastingDebugDay()
        {
            this.Start = Vector3.zero;
            this.Direction = Vector3.zero;
            this.Color = Color.black;
            this.show = false;
        }

        public LastingDebugDay(Vector3 start, Vector3 direction, Color color)
        {
            this.Start = start;
            this.Direction = direction;
            this.Color = color;
        }

        public void Set(Vector3 start, Vector3 direction, Color color, bool show)
        {
            this.Start = start;
            this.Direction = direction;
            this.Color = color;
            this.show = show;
        }

        public void Draw()
        {
            if (!show) return;
            Debug.DrawRay(Start, Direction, Color);
        }

        public void Hide()
        {
            show = false;
        }
    }
}

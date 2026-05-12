using System.Collections.Generic;
using _Game.Shared;
using UnityEngine;

namespace _Game.Gameplay
{
    [RequireComponent(typeof(LineRenderer))]
    public class BeamVisualController : MonoBehaviour
    {
        private readonly List<LineRenderer> _renderers = new();

        private void Awake()
        {
            BeamVisualShared.Beams.Clear();

            LineRenderer line = GetComponent<LineRenderer>();
            line.positionCount = 2;
            line.enabled = false;
            _renderers.Add(line);
        }

        private void Update()
        {
            List<BeamVisualData> beams = BeamVisualShared.Beams;

            // Grow pool as needed
            while (_renderers.Count < beams.Count)
            {
                LineRenderer source = _renderers[0];
                GameObject go = new("BeamVisual");
                go.transform.SetParent(transform, false);
                LineRenderer line = go.AddComponent<LineRenderer>();
                line.material = source.material;
                line.positionCount = 2;
                line.enabled = false;
                _renderers.Add(line);
            }

            // Update active renderers
            for (int i = 0; i < beams.Count; i++)
            {
                BeamVisualData beam = beams[i];
                LineRenderer line = _renderers[i];
                line.enabled = true;
                line.startWidth = beam.Width;
                line.endWidth = beam.Width;
                line.SetPosition(0, beam.Origin);
                line.SetPosition(1, beam.End);
            }

            // Disable unused renderers
            for (int i = beams.Count; i < _renderers.Count; i++)
            {
                _renderers[i].enabled = false;
            }
        }
    }
}

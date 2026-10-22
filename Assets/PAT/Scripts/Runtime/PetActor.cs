using System.Collections.Generic;
using UnityEngine;

namespace Pat.ArLab
{
    public sealed class PetActor : MonoBehaviour
    {
        private readonly List<Material> materials = new List<Material>();
        private Transform body;
        private Vector3 basePosition;
        private float phase;
        private bool highlighted;
        public PetDefinition Definition { get; private set; }

        public void Initialize(PetDefinition definition)
        {
            Definition = definition;
            phase = Random.Range(0f, 6.28f);
            basePosition = transform.localPosition;
            body = new GameObject("Pet body").transform;
            body.SetParent(transform, false);
            Part("Body", PrimitiveType.Sphere, Vector3.zero, new Vector3(0.13f, 0.105f, 0.15f), definition.color);
            Part("Head", PrimitiveType.Sphere, new Vector3(0f, 0.02f, -0.075f), Vector3.one * 0.08f, new Color(0.19f, 0.23f, 0.20f));
            Part("Eye left", PrimitiveType.Sphere, new Vector3(-0.023f, 0.04f, -0.105f), Vector3.one * 0.023f, Color.white);
            Part("Eye right", PrimitiveType.Sphere, new Vector3(0.023f, 0.04f, -0.105f), Vector3.one * 0.023f, Color.white);
            Part("Pupil left", PrimitiveType.Sphere, new Vector3(-0.023f, 0.04f, -0.115f), Vector3.one * 0.011f, Color.black);
            Part("Pupil right", PrimitiveType.Sphere, new Vector3(0.023f, 0.04f, -0.115f), Vector3.one * 0.011f, Color.black);

            if (definition.id == "ladybug")
            {
                for (var i = 0; i < 5; i++)
                {
                    var angle = i * 1.7f;
                    Part("Spot", PrimitiveType.Sphere, new Vector3(Mathf.Sin(angle) * 0.043f, 0.045f, Mathf.Cos(angle) * 0.045f), new Vector3(0.028f, 0.012f, 0.028f), new Color(0.12f, 0.15f, 0.14f));
                }
            }
            else if (definition.id == "hunter-bee")
            {
                Part("Wing left", PrimitiveType.Sphere, new Vector3(-0.08f, 0.06f, 0.02f), new Vector3(0.1f, 0.015f, 0.06f), new Color(0.8f, 0.94f, 1f));
                Part("Wing right", PrimitiveType.Sphere, new Vector3(0.08f, 0.06f, 0.02f), new Vector3(0.1f, 0.015f, 0.06f), new Color(0.8f, 0.94f, 1f));
                Part("Stripe", PrimitiveType.Sphere, new Vector3(0, 0.006f, 0.025f), new Vector3(0.132f, 0.107f, 0.035f), new Color(0.20f, 0.18f, 0.12f));
            }
            else
            {
                Part("Tail 1", PrimitiveType.Sphere, new Vector3(0f, -0.015f, 0.09f), Vector3.one * 0.085f, definition.color * 0.85f);
                Part("Tail 2", PrimitiveType.Sphere, new Vector3(0f, -0.025f, 0.15f), Vector3.one * 0.065f, definition.color * 0.7f);
            }

            var target = gameObject.AddComponent<SphereCollider>();
            target.radius = 0.115f;
        }

        private void Part(string partName, PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = partName;
            part.transform.SetParent(body, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            Destroy(part.GetComponent<Collider>());
            var material = new Material(Shader.Find("Standard"));
            material.color = color;
            material.SetFloat("_Glossiness", 0.18f);
            part.GetComponent<Renderer>().sharedMaterial = material;
            materials.Add(material);
        }

        public void SetHighlighted(bool value)
        {
            if (highlighted == value) return;
            highlighted = value;
            transform.localScale = Vector3.one * (value ? 1.12f : 1f);
        }

        private void Update()
        {
            transform.localPosition = basePosition + Vector3.up * (Mathf.Sin(Time.time * 2.5f + phase) * 0.018f);
            body.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.time + phase) * 20f, Mathf.Sin(Time.time * 2f + phase) * 5f);
        }

        private void OnDestroy()
        {
            foreach (var material in materials) if (material != null) Destroy(material);
        }
    }
}

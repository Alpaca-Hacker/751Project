using UnityEngine;

namespace SoftBody.Scripts.Dropper
{
    public class SimpleDropTest : MonoBehaviour
    {
        public SoftBodyPool pool;
        public Transform dropPoint;
        public float dropInterval = 1f;
    
        private void Start()
        {
            for (var i = 0; i < 5; i++)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.transform.position = new Vector3(i * 2 - 4, -i * 0.5f, 0);
                sphere.transform.localScale = Vector3.one * 2;
               // sphere.GetComponent<Renderer>().enabled = false;
            }
        
            InvokeRepeating(nameof(DropToy), 1f, dropInterval);
        }
    
        private void DropToy()
        {
            var toy = pool.GetObject();
            if (toy != null)
            {
                toy.transform.position = dropPoint.position + Random.insideUnitSphere * 0.5f;
            }
        }
    }
}
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace AmazingAssets.Beast.ExampleScripts
{
    public class SceneManager : MonoBehaviour
    {
        public Renderer targetMesh;

        public Material[] materials;

        public GameObject direactionalLight;
        public GameObject pointLights;


        int usedMaterialIndex = 0;
        bool tessellationON = true;

        // Use this for initialization
        void Start()
        {
            usedMaterialIndex = 0;
        }

        void Update()
        {
            if(Input.GetKeyDown(KeyCode.Space))
            {
                ChangeMaterial();
            }
        }



        public void UIButton_ChangeMaterial()
        {
            ChangeMaterial();
        }

        public void UIButton_ToggleLight()
        {
            ToggleLight();
        }

        public void UIButton_ToggleTessellation()
        {
            ToggleTesselation();
        }


        void ChangeMaterial()
        {
            usedMaterialIndex += 1;

            if (usedMaterialIndex >= materials.Length)
                usedMaterialIndex = 0;

            targetMesh.sharedMaterial = materials[usedMaterialIndex];
        }

        void ToggleLight()
        {
            if (pointLights.activeSelf)
            {
                direactionalLight.SetActive(true);
                pointLights.SetActive(false);                
            }
            else
            {
                direactionalLight.SetActive(false);
                pointLights.SetActive(true);
            }
        }

        void ToggleTesselation()
        {
            tessellationON = !tessellationON;

            foreach (var item in materials)
            {
                item.SetFloat("_Beast_TessellationFactor", tessellationON ? 64 : 1);

                if (tessellationON)
                    item.EnableKeyword("_NORMALMAP");
                else
                    item.DisableKeyword("_NORMALMAP");
            }            
        }
    }
}

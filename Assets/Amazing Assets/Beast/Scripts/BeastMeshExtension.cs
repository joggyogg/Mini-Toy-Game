// Beast - Advanced Tessellation Shader <http://u3d.as/JxL>
// Copyright (c) Amazing Assets <https://amazingassets.world>
 
using System.Collections.Generic;

using UnityEngine;


namespace AmazingAssets.Beast
{
    static public class BeastMeshExtension
    {
        public static Mesh GenerateSmoothNormals(this Mesh sourceMesh)
        {
            if (sourceMesh == null)
            {
                Debug.LogError("Generating mesh with smooth normals has failed. Mesh is null.");
                return null;
            }
            if (sourceMesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.Normal) == false)
            {
                Debug.LogError($"Generating mesh with smooth normals has failed. Mesh '{sourceMesh}' has no normals.", sourceMesh);
                return null;
            }


            Mesh newMesh = UnityEngine.Object.Instantiate(sourceMesh);
            newMesh.name = newMesh.name.Replace("(Clone)", string.Empty);
            newMesh.name += " (Smooth Normals)";

            Vector3[] sourceVertices = sourceMesh.vertices;
            Vector3[] sourceNormals = sourceMesh.normals;

            Dictionary<Vector3, Vector3> smoothNormalsHash = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                Vector3 key = sourceVertices[i];

                if (smoothNormalsHash.ContainsKey(key))
                {
                    smoothNormalsHash[key] = (smoothNormalsHash[key] + sourceNormals[i]).normalized;
                }
                else
                {
                    smoothNormalsHash.Add(key, sourceNormals[i]);
                }
            }


            List<Vector3> smoothNormals = new List<Vector3>(sourceMesh.normals);
            for (int i = 0; i < sourceVertices.Length; i++)
            {
                smoothNormals[i] = smoothNormalsHash[sourceVertices[i]];
            }

            newMesh.SetUVs(3, smoothNormals);

            return newMesh;
        }
    }
}

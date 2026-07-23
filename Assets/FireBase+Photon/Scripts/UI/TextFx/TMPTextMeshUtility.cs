using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace GameUI.TextFx
{
    public static class TMPTextMeshUtility
    {
        public struct CharMeshRef
        {
            public int CharIndex;
            public int MaterialIndex;
            public int VertexIndex;
            public Vector3[] OriPos;
            public Color32 OriColor;
        }

        public static void ForceRefresh(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.ForceMeshUpdate(true);
        }

        public static List<CharMeshRef> CollectVisibleChars(TMP_Text text, bool includeWhitespace)
        {
            var result = new List<CharMeshRef>();
            if (text == null)
            {
                return result;
            }

            ForceRefresh(text);
            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null)
            {
                return result;
            }

            for (int i = 0; i < textInfo.characterCount; i++)
            {
                TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
                if (!charInfo.isVisible)
                {
                    continue;
                }

                char c = charInfo.character;
                bool isWhitespace = char.IsWhiteSpace(c);
                if (isWhitespace && !includeWhitespace)
                {
                    continue;
                }

                int matIndex = charInfo.materialReferenceIndex;
                int vertexIndex = charInfo.vertexIndex;
                TMP_MeshInfo meshInfo = textInfo.meshInfo[matIndex];

                var oriPos = new Vector3[4];
                for (int j = 0; j < 4; j++)
                {
                    oriPos[j] = meshInfo.vertices[vertexIndex + j];
                }

                result.Add(new CharMeshRef
                {
                    CharIndex = i,
                    MaterialIndex = matIndex,
                    VertexIndex = vertexIndex,
                    OriPos = oriPos,
                    OriColor = meshInfo.colors32[vertexIndex]
                });
            }

            return result;
        }

        public static Vector3[] GetOriPos(TMP_MeshInfo meshInfo, int vertexIndex)
        {
            var oriPos = new Vector3[4];
            for (int j = 0; j < 4; j++)
            {
                oriPos[j] = meshInfo.vertices[vertexIndex + j];
            }

            return oriPos;
        }

        public static void SetVertexPosition(
            TMP_Text text,
            int materialIndex,
            int vertexIndex,
            Vector3 pos,
            IReadOnlyList<Vector3> oriPos,
            Color color,
            bool changeColor)
        {
            if (text == null || oriPos == null || oriPos.Count < 4)
            {
                return;
            }

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null || materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length)
            {
                return;
            }

            TMP_MeshInfo meshInfo = textInfo.meshInfo[materialIndex];
            for (int j = 0; j < 4; j++)
            {
                meshInfo.vertices[vertexIndex + j] = oriPos[j] + pos;
                if (changeColor)
                {
                    meshInfo.colors32[vertexIndex + j] = color;
                }
                else
                {
                    Color32 c = meshInfo.colors32[vertexIndex + j];
                    c.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * 255f), 0, 255);
                    meshInfo.colors32[vertexIndex + j] = c;
                }
            }
        }

        public static void ApplyMesh(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        public static void RestoreOriginalMesh(TMP_Text text, List<CharMeshRef> chars)
        {
            if (text == null || chars == null)
            {
                return;
            }

            TMP_TextInfo textInfo = text.textInfo;
            if (textInfo == null)
            {
                return;
            }

            for (int i = 0; i < chars.Count; i++)
            {
                CharMeshRef ch = chars[i];
                if (ch.MaterialIndex < 0 || ch.MaterialIndex >= textInfo.meshInfo.Length || ch.OriPos == null)
                {
                    continue;
                }

                TMP_MeshInfo meshInfo = textInfo.meshInfo[ch.MaterialIndex];
                for (int j = 0; j < 4; j++)
                {
                    meshInfo.vertices[ch.VertexIndex + j] = ch.OriPos[j];
                    Color32 c = ch.OriColor;
                    meshInfo.colors32[ch.VertexIndex + j] = c;
                }
            }

            ApplyMesh(text);
        }
    }
}

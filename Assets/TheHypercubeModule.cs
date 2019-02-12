using System.Collections;
using System.Linq;
using TheHypercube;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of The Hypercube
/// Created by Timwi
/// </summary>
public class TheHypercubeModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public Transform Hypercube;
    public Transform[] Edges;
    public Transform[] Vertices;
    public MeshFilter[] Faces;
    public Mesh Quad;
    public Material FaceMaterial;

    private int[][] _rotations;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        ColorChange();

        _rotations = new int[8][];
        for (int i = 0; i < _rotations.Length; i++)
        {
            var axes = Enumerable.Range(0, 4).ToArray().Shuffle();
            _rotations[i] = new[] { axes[0], axes[1] };
        }
        StartCoroutine(RotateHypercube());
    }

    private void ColorChange()
    {
        var hue = Rnd.Range(0f, 1f);
        var sat = Rnd.Range(.6f, .9f);
        var v = Rnd.Range(.75f, 1f);

        Edges[0].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(hue, sat, v);
        for (int i = 1; i < Edges.Length; i++)
            Edges[i].GetComponent<MeshRenderer>().sharedMaterial = Edges[0].GetComponent<MeshRenderer>().sharedMaterial;

        Vertices[0].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(hue, sat * .8f, v * .5f);
        for (int i = 1; i < Vertices.Length; i++)
            Vertices[i].GetComponent<MeshRenderer>().sharedMaterial = Vertices[0].GetComponent<MeshRenderer>().sharedMaterial;

        var clr = Color.HSVToRGB(hue, sat * .8f, v * .75f);
        clr.a = .3f;
        Faces[0].GetComponent<MeshRenderer>().material.color = clr;
        for (int i = 1; i < Faces.Length; i++)
            Faces[i].GetComponent<MeshRenderer>().sharedMaterial = Faces[0].GetComponent<MeshRenderer>().sharedMaterial;
    }

    private IEnumerator RotateHypercube()
    {
        yield return new WaitForSeconds(Rnd.Range(.1f, 2f));
        while (true)
        {
            for (int rot = 0; rot < _rotations.Length; rot++)
            {
                var axis1 = _rotations[rot][0];
                var axis2 = _rotations[rot][1];
                var duration = 2f;
                var elapsed = 0f;

                var unrotatedVertices = Enumerable.Range(0, 1 << 4).Select(i => new Point4D((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1, (i & 8) != 0 ? 1 : -1)).ToArray();

                while (elapsed < duration)
                {
                    var angle = easeInOutQuad(elapsed, 0, Mathf.PI / 2, duration);
                    var matrix = new double[16];
                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                            matrix[i + 4 * j] =
                                i == axis1 && j == axis1 ? Mathf.Cos(angle) :
                                i == axis1 && j == axis2 ? Mathf.Sin(angle) :
                                i == axis2 && j == axis1 ? -Mathf.Sin(angle) :
                                i == axis2 && j == axis2 ? Mathf.Cos(angle) :
                                i == j ? 1 : 0;

                    SetHypercube(unrotatedVertices.Select(v => (v * matrix).Project()).ToArray());

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                // Reset the position of the hypercube
                SetHypercube(unrotatedVertices.Select(v => v.Project()).ToArray());
                yield return new WaitForSeconds(Rnd.Range(0.5f, 1.5f));
            }
            yield return new WaitForSeconds(Rnd.Range(1f, 2f));
        }
    }

    private static float easeInOutQuad(float t, float start, float end, float duration)
    {
        var change = end - start;
        t /= duration / 2;
        if (t < 1)
            return change / 2 * t * t + start;
        t--;
        return -change / 2 * (t * (t - 2) - 1) + start;
    }

    private void SetHypercube(Vector3[] vertices)
    {
        // VERTICES
        for (int i = 0; i < 1 << 4; i++)
            Vertices[i].localPosition = vertices[i];

        // EDGES
        var e = 0;
        for (int i = 0; i < 1 << 4; i++)
            for (int j = i + 1; j < 1 << 4; j++)
                if (((i ^ j) & ((i ^ j) - 1)) == 0)
                {
                    Edges[e].localPosition = (vertices[i] + vertices[j]) / 2;
                    Edges[e].localRotation = Quaternion.FromToRotation(Vector3.up, vertices[j] - vertices[i]);
                    Edges[e].localScale = new Vector3(.1f, (vertices[j] - vertices[i]).magnitude / 2, .1f);
                    e++;
                }

        // FACES
        var f = 0;
        for (int i = 0; i < 1 << 4; i++)
            for (int j = i + 1; j < 1 << 4; j++)
            {
                var b1 = i ^ j;
                var b2 = b1 & (b1 - 1);
                if (b2 != 0 && (b2 & (b2 - 1)) == 0 && (i & b1 & ((i & b1) - 1)) == 0 && (j & b1 & ((j & b1) - 1)) == 0)
                    Faces[f++].sharedMesh = new Mesh { vertices = new[] { vertices[i], vertices[i | j], vertices[i & j], vertices[j] }, triangles = new[] { 0, 1, 2, 1, 2, 3, 2, 1, 0, 3, 2, 1 } };
            }
    }

    private KMSelectable[] ProcessTwitchCommand(string command)
    {
        return null;
    }
}

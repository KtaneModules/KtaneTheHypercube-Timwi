using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using TheHypercube;
using UnityEngine;

using Rnd = UnityEngine.Random;

/// <summary>
/// On the Subject of Hypermatch
/// Created by Goofy
/// Original module by Timwi
/// </summary>
public class HypermatchModule : MonoBehaviour
{
    public KMBombInfo Bomb;
    public KMBombModule Module;
    public KMAudio Audio;
    public Transform Hypercube;
    public Transform[] Edges;
    public KMSelectable[] Vertices;
    public MeshFilter Faces;
    public Mesh Quad;

    private static int _moduleIdCounter = 1;
    private int _moduleId;

    private float _hue, _sat, _v;
    private Coroutine _rotationCoroutine;
    private bool _transitioning;
    private bool _rotating;
    private int _progress;
    private List<int> _vertexColors;
    private int? _pair1;
    private readonly List<int> _pressedVertices = new List<int>();

    // Long-press handling
    private bool _isButtonDown;
    private Coroutine _buttonDownCoroutine;

    private Material _edgesMat, _facesMat;
    private Mesh _lastFacesMesh = null;
    private static readonly string[][] _dimensionNames = new[] { new[] { "left", "right" }, new[] { "bottom", "top" }, new[] { "front", "back" }, new[] { "zig", "zag" } };
    private static readonly string[] _colorNames = new[] { "red", "yellow", "green", "blue", "purple", "pink", "white", "black" };
    private static readonly Color[] _vertexColorValues = "e54747,e5e347,47e547,3ba0f1,911eb4,fa8ea4,ffffff,000000".Split(',').Select(str => new Color(Convert.ToInt32(str.Substring(0, 2), 16) / 255f, Convert.ToInt32(str.Substring(2, 2), 16) / 255f, Convert.ToInt32(str.Substring(4, 2), 16) / 255f)).ToArray();
    private static readonly int[] _shapeOrder = { 3, 1, 2, 0 };

    void Start()
    {
        _moduleId = _moduleIdCounter++;

        _edgesMat = Edges[0].GetComponent<MeshRenderer>().material;
        for (int i = 0; i < Edges.Length; i++)
            Edges[i].GetComponent<MeshRenderer>().sharedMaterial = _edgesMat;

        _facesMat = Faces.GetComponent<MeshRenderer>().material;

        SetHypercube(GetUnrotatedVertices().Select(p => p.Project()).ToArray());

        // GENERATE PUZZLE
        _vertexColors = new List<int>() { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5, 6, 6, 7, 7 }.Shuffle();

        for (var i = 0; i < 1 << 4; i++)
        {
            Vertices[i].OnInteract = VertexClick(i);
            Vertices[i].OnInteractEnded = VertexRelease(i);
        }

        for (var vertex = 0; vertex < 1 << 4; vertex++)
            Debug.LogFormat(@"[Hypermatch #{0}] {1} is {2}.", _moduleId, StringifyShape(vertex), _colorNames[_vertexColors[vertex]]);

        _rotationCoroutine = StartCoroutine(RotateHypercube(atStart: true));
    }

    private Point4D[] GetUnrotatedVertices()
    {
        return Enumerable.Range(0, 1 << 4).Select(i => new Point4D((i & 1) != 0 ? 1 : -1, (i & 2) != 0 ? 1 : -1, (i & 4) != 0 ? 1 : -1, (i & 8) != 0 ? 1 : -1)).ToArray();
    }

    private KMSelectable.OnInteractHandler VertexClick(int v)
    {
        return delegate
        {
            Vertices[v].AddInteractionPunch(.2f);
            if (!_transitioning && _progress < 8)
                _buttonDownCoroutine = StartCoroutine(HandleLongPress(v));
            return false;
        };
    }

    private IEnumerator HandleLongPress(int v)
    {
        if (_transitioning || _progress == 8)
            yield break;

        _isButtonDown = true;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, Vertices[v].transform);

        yield return new WaitForSeconds(.7f);
        _isButtonDown = false;
        _buttonDownCoroutine = null;
        Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, Vertices[v].transform);

        // Handle long press
        if (_rotationCoroutine == null && _progress < 8)
        {
            for (int i = 0; i < Vertices.Length; i++)
                Vertices[i].GetComponent<MeshRenderer>().material.color = _vertexColorValues[_vertexColors[i]];
            _pressedVertices.Clear();
            _rotationCoroutine = StartCoroutine(RotateHypercube());
            _pair1 = null;
        }
    }

    private Action VertexRelease(int v)
    {
        return delegate
        {
            if (!_isButtonDown || _progress == 8) // Long press already handled by HandleLongPress()
                return;
            _isButtonDown = false;

            if (_buttonDownCoroutine != null)
            {
                StopCoroutine(_buttonDownCoroutine);
                _buttonDownCoroutine = null;
            }

            if (_pressedVertices.Contains(v))
                return;

            // Handle short press
            if (_rotationCoroutine != null)
                StartCoroutine(ColorChange(noVertices: true));
            else if (_pair1 == null && _rotationCoroutine == null)
            {
                _pressedVertices.Add(v);
                _pair1 = v;
                Vertices[v].GetComponent<MeshRenderer>().material.color = _vertexColorValues[_vertexColors[v]];
            }
            else if (_pair1 != null && _rotationCoroutine == null)
            {
                if (_vertexColors[v] == _vertexColors[_pair1.Value])
                {
                    Debug.LogFormat(@"[Hypermatch #{0}] {1} correctly paired with {2}.", _moduleId, StringifyShape(_pair1.Value), StringifyShape(v));
                    Vertices[v].GetComponent<MeshRenderer>().material.color = _vertexColorValues[_vertexColors[v]];
                    _pressedVertices.Add(v);
                    _progress++;
                    _pair1 = null;
                    Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
                    if (_progress == 8)
                    {
                        Debug.LogFormat(@"[Hypermatch #{0}] Module solved.", _moduleId);
                        Module.HandlePass();
                        StartCoroutine(ColorChange(keepGrey: true));
                    }
                }
                else
                {
                    Debug.LogFormat(@"[Hypermatch #{0}] {1} incorrectly paired with {2}. Strike!", _moduleId, StringifyShape(_pair1.Value), StringifyShape(v));
                    _progress = 0;
                    _pair1 = null;
                    Module.HandleStrike();

                    for (int i = 0; i < Vertices.Length; i++)
                        Vertices[i].GetComponent<MeshRenderer>().material.color = _vertexColorValues[_vertexColors[i]];
                    _pressedVertices.Clear();
                    _rotationCoroutine = StartCoroutine(RotateHypercube());
                }
            }
        };
    }

    private string StringifyShape(bool?[] shape)
    {
        var strs = _shapeOrder.Select(d => shape[d] == null ? null : _dimensionNames[d][shape[d].Value ? 1 : 0]).Where(s => s != null).ToArray();
        if (strs.Length == 0)
            return "hypercube";
        return strs.Join("-") + " " + (
            strs.Length == 1 ? "cube" :
            strs.Length == 2 ? "face" :
            strs.Length == 3 ? "edge" : "vertex");
    }
    private string StringifyShape(int vertex)
    {
        return StringifyShape(Enumerable.Range(0, 4).Select(d => (bool?) ((vertex & (1 << d)) != 0)).ToArray());
    }

    private IEnumerator ColorChange(bool keepGrey = false, bool atStart = false, bool noVertices = false)
    {
        _transitioning = true;

        var prevHue = .5f;
        var prevSat = 0f;
        var prevV = .5f;
        SetCubeColor(prevHue, prevSat, prevV);
        if (atStart)
            SetVertexColors(prevHue, prevSat, prevV);

        if (!keepGrey)
        {
            _hue = .5f;
            _sat = 0;
            _v = .5f;

            yield return new WaitForSeconds((atStart ? .22f : 2.22f) + Rnd.Range(0.5f, 1.5f));

            _hue = Rnd.Range(0f, 1f);
            _sat = Rnd.Range(.6f, .9f);
            _v = Rnd.Range(.75f, 1f);

            var duration = 1.5f;
            var elapsed = 0f;
            while (elapsed < duration)
            {
                SetCubeColor(Mathf.Lerp(prevHue, _hue, elapsed / duration), Mathf.Lerp(prevSat, _sat, elapsed / duration), Mathf.Lerp(prevV, _v, elapsed / duration));
                if (atStart || !_rotating)
                    SetVertexColors(Mathf.Lerp(prevHue, _hue, elapsed / duration), Mathf.Lerp(prevSat, _sat, elapsed / duration), Mathf.Lerp(prevV, _v, elapsed / duration));
                yield return null;
                elapsed += Time.deltaTime;
            }
            SetCubeColor(_hue, _sat, _v);
            if (atStart || !_rotating)
                SetVertexColors(_hue, _sat, _v);

            if (!noVertices)
                for (int v = 0; v < Vertices.Length; v++)
                    Vertices[v].GetComponent<MeshRenderer>().material.color = _vertexColorValues[_vertexColors[v]];
        }

        while (_rotating)
            yield return null;
        _transitioning = false;
        PlayRandomSound();
    }

    private void SetCubeColor(float h, float s, float v)
    {
        _edgesMat.color = Color.HSVToRGB(h, s, v);
        var clr = Color.HSVToRGB(h, s * .8f, v * .75f);
        clr.a = .1f;
        _facesMat.color = clr;
    }

    private void SetVertexColors(float h, float s, float v)
    {
        for (int i = 0; i < Vertices.Length; i++)
            Vertices[i].GetComponent<MeshRenderer>().material.color = Color.HSVToRGB(h, s * .8f, v * .5f);
    }

    private IEnumerator RotateHypercube(bool atStart = false)
    {
        var colorChange = ColorChange(atStart: atStart);
        while (colorChange.MoveNext())
            yield return colorChange.Current;

        _rotating = true;

        var unrotatedVertices = GetUnrotatedVertices();
        SetHypercube(unrotatedVertices.Select(v => v.Project()).ToArray());

        while (_rotating)
        {
            while (_rotating)
            {
                var duration = 8f;
                var elapsed = 0f;

                // Random rotation axis
                var a = new List<double> { Rnd.Range(0f, 1f), Rnd.Range(0f, 1f), Rnd.Range(0f, 1f) };
                var X = a[0];
                var Y = a[1];
                var Z = a[2];
                var d = Math.Sqrt(X * X + Y * Y + Z * Z);
                for (var i = 0; i < 3; i++)
                    a[i] /= 1.5 * d;
                var ea = Rnd.Range(0, 4);
                var tr = Enumerable.Range(0, 4).Select(ax => ax > ea ? ax - 1 : ax).ToArray();
                var trr = Enumerable.Range(0, 3).Select(ax => ax < ea ? ax : ax + 1).ToArray();
                a.Insert(ea, 0);

                while (elapsed < duration)
                {
                    var angle = 2 * Mathf.PI * elapsed / duration;
                    var sin = Math.Sin(angle);
                    var cos = Math.Cos(angle);
                    var mcos = 1 - Math.Cos(angle);
                    var matrix = new double[16];

                    for (int i = 0; i < 4; i++)
                        for (int j = 0; j < 4; j++)
                            matrix[i + 4 * j] =
                                i == j && i == ea ? 1 : i == ea || j == ea ? 0 :
                                i == j ? a[i] * a[i] * mcos + cos :
                                a[i] * a[j] * mcos + a[trr[3 - tr[i] - tr[j]]] * (i % 2 != 0 ^ j % 2 != 0 ? -1 : 1) * sin;

                    SetHypercube(unrotatedVertices.Select(v => (v * matrix).Project()).ToArray());

                    yield return null;
                    elapsed += Time.deltaTime;
                }

                // Reset the position of the hypercube
                SetHypercube(unrotatedVertices.Select(v => v.Project()).ToArray());
                if (_transitioning)
                    _rotating = false;
            }
        }

        SetVertexColors(_hue, _sat, _v);
        _rotationCoroutine = null;
    }

    private void SetHypercube(Vector3[] vertices)
    {
        // VERTICES
        for (int i = 0; i < 1 << 4; i++)
            Vertices[i].transform.localPosition = vertices[i];

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
        if (_lastFacesMesh != null)
            Destroy(_lastFacesMesh);

        var f = 0;
        var triangles = new List<int>();
        for (int i = 0; i < 1 << 4; i++)
            for (int j = i + 1; j < 1 << 4; j++)
            {
                var b1 = i ^ j;
                var b2 = b1 & (b1 - 1);
                if (b2 != 0 && (b2 & (b2 - 1)) == 0 && (i & b1 & ((i & b1) - 1)) == 0 && (j & b1 & ((j & b1) - 1)) == 0)
                {
                    triangles.AddRange(new[] { i, i | j, i & j, i | j, i & j, j, i & j, i | j, i, j, i & j, i | j });
                    f++;
                }
            }
        _lastFacesMesh = new Mesh { vertices = vertices, triangles = triangles.ToArray() };
        _lastFacesMesh.RecalculateNormals();
        Faces.sharedMesh = _lastFacesMesh;
    }

    private void PlayRandomSound()
    {
        Audio.PlaySoundAtTransform("Bleep" + Rnd.Range(1, 11), transform);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"!{0} go [use when hypercube is rotating] | !{0} zig-bottom-front-left [presses a vertex when the hypercube is not rotating] | !{0} reset [forget input and resume rotations]";
#pragma warning restore 414

    IEnumerator ProcessTwitchCommand(string command)
    {
        if (_rotationCoroutine != null && Regex.IsMatch(command, @"^\s*(go|activate|stop|run|start|on|off)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            yield return new[] { Vertices[0] };
            yield break;
        }

        if (_rotationCoroutine == null && Regex.IsMatch(command, @"^\s*(reset|go back|return|resume|rotate|rotations|cancel|abort)\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            yield return null;
            Vertices[0].OnInteract();
            yield return new WaitForSeconds(1f);
            Vertices[0].OnInteractEnded();
            yield break;
        }

        Match m;
        if (_rotationCoroutine == null && (m = Regex.Match(command, string.Format(@"^\s*((?:{0})(?:[- ,;]*(?:{0}))*)\s*$", _dimensionNames.SelectMany(x => x).Join("|")), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).Success)
        {
            var elements = m.Groups[1].Value.Split(new[] { ' ', ',', ';', '-' }, StringSplitOptions.RemoveEmptyEntries);
            if (elements.Length != 4)
            {
                yield return "sendtochaterror It’s a 4D hypercube, you gotta have 4 dimensions.";
                yield break;
            }
            var dimensions = elements.Select(el => _dimensionNames.IndexOf(d => d.Any(dn => dn.EqualsIgnoreCase(el)))).ToArray();
            var invalid = Enumerable.Range(0, 3).SelectMany(i => Enumerable.Range(i + 1, 3 - i).Where(j => dimensions[i] == dimensions[j]).Select(j => new { i, j })).FirstOrDefault();
            if (invalid != null)
            {
                yield return elements[invalid.i].EqualsIgnoreCase(elements[invalid.j])
                    ? string.Format("sendtochaterror You wrote “{0}” twice.", elements[invalid.i], elements[invalid.j])
                    : string.Format("sendtochaterror “{0}” and “{1}” doesn’t jive.", elements[invalid.i], elements[invalid.j]);
                yield break;
            }
            var vertexIx = 0;
            for (int i = 0; i < 4; i++)
                vertexIx |= _dimensionNames[dimensions[i]].IndexOf(dn => dn.EqualsIgnoreCase(elements[i])) << dimensions[i];
            yield return null;
            yield return new[] { Vertices[vertexIx] };
        }
    }

    IEnumerator TwitchHandleForcedSolve()
    {
        if (_rotationCoroutine != null)
        {
            Vertices[0].OnInteract();
            yield return new WaitForSeconds(.1f);
            Vertices[0].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }

        while (_progress < 8)
        {
            while (_transitioning)
                yield return true;
            yield return new WaitForSeconds(.1f);

            var firstUnpressed = _pair1 ?? Enumerable.Range(0, 1 << 4).First(v => !_pressedVertices.Contains(v));
            var match = Enumerable.Range(0, 1 << 4).First(v => v != firstUnpressed && _vertexColors[v] == _vertexColors[firstUnpressed]);

            Vertices[firstUnpressed].OnInteract();
            yield return new WaitForSeconds(.1f);
            Vertices[firstUnpressed].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
            Vertices[match].OnInteract();
            yield return new WaitForSeconds(.1f);
            Vertices[match].OnInteractEnded();
            yield return new WaitForSeconds(.1f);
        }

        while (_transitioning)
            yield return true;
    }
}
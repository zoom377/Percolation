using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.SymbolStore;
using TMPro;
using Unity.Profiling;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = UnityEngine.Random;

public class Percolation : MonoBehaviour
{
    [SerializeField] Renderer _renderer;
    [SerializeField] float _lerp, _keyChangeRate;
    [SerializeField] GUIStyle _textStyle;

    int _width = 256, _height = 256;
    float _pValue = .5f, _desiredPValue = .5f;
    Texture2D _texture;
    Node[] _nodes;
    Color[] _colors;
    int[] _seeds;
    Vector2Int[] _resolutions = new Vector2Int[]
    {
        new Vector2Int(32,32),
        new Vector2Int(64,64),
        new Vector2Int(128,128),
        new Vector2Int(256,256),
        new Vector2Int(512,512),
    };
    int _seedIndex = 0, _resolutionIndex = 2;


    void Start()
    {
        _resolutionIndex = PlayerPrefs.GetInt("ResolutionIndex", 2);

        _seedIndex = -1;
        _seeds = new int[10_000];
        for (int i = 0; i < _seeds.Length; i++)
        {
            _seeds[i] = Random.Range(int.MinValue, int.MaxValue);
        }

        Initialise();
        StartCoroutine(UpdateTextureContinuously());
    }

    private void Initialise()
    {
        _width = _resolutions[_resolutionIndex].x;
        _height = _resolutions[_resolutionIndex].y;

        _texture = new Texture2D(_width, _height)
        {
            filterMode = FilterMode.Point
        };

        _nodes = new Node[_width * _height];
        _colors = new Color[_width * _height];

        _seedIndex++;
        if (_seedIndex >= _seeds.Length)
            _seedIndex = 0;

        Random.InitState(_seeds[_seedIndex]);
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new Node { BottomChance = Random.Range(0f, 1f), RightChance = Random.Range(0f, 1f) };
        }
    }

    private void Update()
    {

        if (Input.GetKeyDown(KeyCode.Escape) && Application.platform != RuntimePlatform.WebGLPlayer)
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Initialise();
        }
        if (Input.GetKey(KeyCode.UpArrow))
        {
            _desiredPValue += _keyChangeRate * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.DownArrow))
        {
            _desiredPValue -= _keyChangeRate * Time.deltaTime;
        }

        if (Input.GetKeyDown(KeyCode.Equals))
        {
            if (_resolutionIndex < _resolutions.Length - 1)
            {
                _resolutionIndex++;
                PlayerPrefs.SetInt("ResolutionIndex", _resolutionIndex);
                Initialise();
            }
        }
        else if (Input.GetKeyDown(KeyCode.Minus))
        {
            if (_resolutionIndex > 0)
            {
                _resolutionIndex--;
                PlayerPrefs.SetInt("ResolutionIndex", _resolutionIndex);
                Initialise();
            }
        }

        _desiredPValue = Mathf.Clamp01(_desiredPValue);

        //Smooth out changes to p value
        _pValue = Mathf.Lerp(_pValue, _desiredPValue, _lerp * Time.deltaTime);
    }

    private void OnGUI()
    {

        GUILayout.Label($"Resolution: {_width}x{_height}" +
            $"\nFPS: {(1 / Time.smoothDeltaTime).ToString("0")}" +
            $"\nP value: {_pValue.ToString("0.0000")}",
            _textStyle);

        GUILayout.BeginArea(new Rect(20, 100, Screen.width * 0.2f, Screen.height - 120));
        _desiredPValue = 1 - GUILayout.VerticalSlider(1 - _desiredPValue, 0f, 1f);
        GUILayout.EndArea();
    }

    IEnumerator UpdateTextureContinuously()
    {
        while (true)
        {
            UpdateTexture();
            yield return new WaitForEndOfFrame();
        }
    }

    unsafe void UpdateTexture()
    {
        //Unsafe stuff hopefully removes bounds checking for performance
        //  Flood fill gets called about 30,000 times per frame with a 256x256 texture
        //  Didn't notice a huge difference from the fixed scope.
        fixed (Color* colors = _colors)
        {
            fixed (Node* nodes = _nodes)
            {
                UpdateLinkOpenness();

                //Reset visited status of all nodes
                for (int i = 0; i < _nodes.Length; i++)
                {
                    nodes[i].Visited = false;
                }


                var stack = new Stack<(int, int)>();
                for (int y = 0; y < _height; y++)
                {
                    for (int x = 0; x < _width; x++)
                    {
                        if (false == nodes[y * _width + x].Visited)
                        {
                            FloodFill(nodes, colors, (x, y), GetRandomColor(y * _width + x), stack);
                        }
                    }
                }

                _texture.SetPixels(_colors);
                _texture.Apply();
                _renderer.material.mainTexture = _texture;
            }
        }
    }

    unsafe void FloodFill(Node* nodes, Color* colors, (int x, int y) pos, Color color, Stack<(int, int)> stack)
    {
        stack.Push(pos);

        //Tuples were significantly faster than Vector2Ints (In editor at least).
        (int x, int y) adj;

        //This loop runs about 60,000 times per frame
        while (stack.TryPop(out pos))
        {
            nodes[pos.y * _width + pos.x].Visited = true;
            colors[pos.y * _width + pos.x] = color;

            //Previously had a function that converted 2d coords to flattened array index.
            //  Was much slower than just inlining.

            //left
            adj = (pos.x - 1, pos.y);
            if (adj.x >= 0 &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[adj.y * _width + adj.x].RightOpen)
            {
                stack.Push(adj);
            }

            //right
            adj = (pos.x + 1, pos.y);
            if (adj.x < _width &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[pos.y * _width + pos.x].RightOpen)
            {
                stack.Push(adj);
            }

            //down
            adj = (pos.x, pos.y - 1);
            if (adj.y >= 0 &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[pos.y * _width + pos.x].BottomOpen)
            {
                stack.Push(adj);
            }

            //up
            adj = (pos.x, pos.y + 1);
            if (adj.y < _height &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[adj.y * _width + adj.x].BottomOpen)
            {
                stack.Push(adj);
            }
        }
    }

    void UpdateLinkOpenness()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].RightOpen = _nodes[i].RightChance < _pValue ? true : false;
            _nodes[i].BottomOpen = _nodes[i].BottomChance < _pValue ? true : false;
        }
    }
    Color GetRandomColor(int count)
    {
        Random.InitState(_seeds[_seedIndex] + count);

        return new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            1f);
    }


    struct Node
    {
        public bool Visited;
        public float RightChance;
        public float BottomChance;
        public bool RightOpen;
        public bool BottomOpen;
    }
}

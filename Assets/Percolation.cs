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
    [SerializeField] float _lerpSpeed;
    [SerializeField] int _width = 256, _height = 256;

    float _pValue = 0, _desiredPValue = 0;
    Texture2D _texture;
    Node[] _nodes;
    Color[] _colors;


    void Start()
    {
        Initialise();
        StartCoroutine(UpdateTextureContinuously());
    }

    private void Initialise()
    {
        _texture = new Texture2D(_width, _height)
        {
            filterMode = FilterMode.Point
        };

        _nodes = new Node[_width * _height];
        _colors = new Color[_width * _height];

        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new Node { BottomChance = Random.Range(0f, 1f), RightChance = Random.Range(0f, 1f) };
        }
    }

    private void Update()
    {
        //Smooth out user input
        _pValue = Mathf.Lerp(_pValue, _desiredPValue, _lerpSpeed * Time.deltaTime);

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Application.Quit();
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            Initialise();
        }
    }

    private void OnGUI()
    {
        GUILayout.Label($"Frame time: {Time.deltaTime.ToString("0.000")}\nFPS: {(1 / Time.smoothDeltaTime).ToString("0")}\nP value: {_pValue.ToString("0.000")}");

        GUILayout.BeginArea(new Rect(20, 80, Screen.width * 0.2f, Screen.height - 100));
        _desiredPValue = GUILayout.VerticalSlider(_desiredPValue, 0f, 1f);
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
    static Color GetRandomColor(int count)
    {
        Random.InitState(count);

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

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
    [SerializeField] float _pValue = 0;
    [SerializeField] float _rate;

    const int _width = 256, _height = 256;

    Texture2D _texture;
    Node[] _nodes;
    Color[] _colors;


    static readonly ProfilerMarker pmInitArrays = new ProfilerMarker("InitArrays");
    static readonly ProfilerMarker pmLoopInitNodes = new ProfilerMarker("LoopInitNodes");
    void Start()
    {
        _texture = new Texture2D(_width, _height);
        _nodes = new Node[_width * _height];
        _colors = new Color[_width * _height];

        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i] = new Node { BottomChance = Random.Range(0f, 1f), RightChance = Random.Range(0f, 1f) };
        }

        StartCoroutine(UpdateTexture());
    }

    IEnumerator UpdateTexture()
    {
        while (_pValue <= 1f)
        {
            _pValue += _rate * Time.deltaTime;
            UpdateLinkOpenness();
            GenerateTexture();
            yield return new WaitForEndOfFrame();
        }
    }

    static readonly ProfilerMarker pm1 = new ProfilerMarker("ResetNode");
    static readonly ProfilerMarker pm2 = new ProfilerMarker("SearchForUnVisited");
    static readonly ProfilerMarker pmApplyTexture = new ProfilerMarker("ApplyTexture");
    static readonly ProfilerMarker pmSetTexture = new ProfilerMarker("SetTexture");
    static readonly ProfilerMarker pmFloodFill = new ProfilerMarker("FloodFill");
    static readonly ProfilerMarker pmSetPixels = new ProfilerMarker("SetPixel");
    static readonly ProfilerMarker pmPop = new ProfilerMarker("Pop");

    unsafe void GenerateTexture()
    {
        fixed (Color* colors = _colors)
        {
            fixed (Node* nodes = _nodes)
            {

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
                        if (false == nodes[Array2DToIndex(new Vector2Int(x, y), _width)].Visited)
                        {
                            FloodFill(nodes, colors, (x, y), GetRandomColor(), stack);
                        }
                    }
                }

                _texture.SetPixels(_colors);
                _texture.Apply();
                _renderer.material.mainTexture = _texture;
            }
        }
    }

    static readonly ProfilerMarker pmCheckAdjacent = new ProfilerMarker("CheckAdjacent");
    static readonly ProfilerMarker pmSetColorAndVisited = new ProfilerMarker("SetColorAndVisited");
    static readonly ProfilerMarker pmPush = new ProfilerMarker("Push");

    unsafe void FloodFill(Node* nodes, Color* colors, (int x, int y) pos, Color color, Stack<(int, int)> stack)
    {
        using var pscope = pmFloodFill.Auto();

        stack.Push(pos);
        (int x, int y) adj;

        while (stack.TryPop(out pos))
        {
            using (var pm1 = pmSetColorAndVisited.Auto())
            {
                nodes[pos.y * _width + pos.x].Visited = true;
                colors[pos.y * _width + pos.x] = color;
            }

            using var pm = pmCheckAdjacent.Auto();

            //left
            adj = (pos.x - 1, pos.y);
            if (adj.x >= 0 &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[adj.y * _width + adj.x].RightOpen)
            {
                //Left is a node
                using var pm2 = pmPush.Auto();
                stack.Push(adj);
            }

            //right
            //adj = pos + Vector2Int.right;
            adj = (pos.x + 1, pos.y);
            if (adj.x < _width &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[pos.y * _width + pos.x].RightOpen)
            {
                //Left is a node
                using var pm2 = pmPush.Auto();
                stack.Push(adj);
            }

            //down
            //adj = pos + Vector2Int.down;
            adj = (pos.x, pos.y - 1);
            if (adj.y >= 0 &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[pos.y * _width + pos.x].BottomOpen)
            {
                //Left is a node
                using var pm2 = pmPush.Auto();
                stack.Push(adj);
            }

            //up
            //adj = pos + Vector2Int.up;
            adj = (pos.x, pos.y + 1);
            if (adj.y < _height &&
                false == nodes[adj.y * _width + adj.x].Visited &&
                nodes[adj.y * _width + adj.x].BottomOpen)
            {
                //Left is a node
                using var pm2 = pmPush.Auto();
                stack.Push(adj);
            }
        }
    }

    private void OnGUI()
    {
        GUILayout.Label($"Frame time:{Time.deltaTime}\nFPS:{1/Time.smoothDeltaTime}");
    }

    static readonly ProfilerMarker pmUpdateLinkOpenness = new ProfilerMarker("pmUpdateLinkOpenness");
    void UpdateLinkOpenness()
    {
        for (int i = 0; i < _nodes.Length; i++)
        {
            _nodes[i].RightOpen = _nodes[i].RightChance < _pValue ? true : false;
            _nodes[i].BottomOpen = _nodes[i].BottomChance < _pValue ? true : false;
        }
    }

    static readonly ProfilerMarker pmGetNode = new ProfilerMarker("GetNode");

    ref Node GetNode(Vector2Int pos)
    {
        using var pm = pmGetNode.Auto();
        return ref _nodes[Array2DToIndex(pos, _width)];
    }

    static int Array2DToIndex(Vector2Int position, int width)
    {
        return position.y * width + position.x;
    }

    static Color GetRandomColor()
    {
        return new Color(
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            Random.Range(0f, 1f),
            1f
            );
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

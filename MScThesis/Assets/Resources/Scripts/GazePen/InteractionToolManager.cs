using UnityEngine;
using System.Collections.Generic;

public enum InteractionTool
{
    Move = 0,
    Delete = 1,
    Duplicate = 2,
    ColorPicker = 3,
    Spawn = 4
}

public class InteractionToolManager : MonoBehaviour
{
    public static InteractionToolManager Instance;

    [Header("Current State")]
    [SerializeField] private InteractionTool currentTool = InteractionTool.Move;

    [Header("UI Menus (Drag Canvases Here)")]
    public GameObject mainMenuCanvas;
    public GameObject colorMenuCanvas;
    public GameObject spawnMenuCanvas;

    [Header("Spawn Settings")]
    [Tooltip("Index 0 = Button 1, Index 1 = Button 2, etc.")]
    public List<GameObject> prefabLibrary; 
    private GameObject prefabToSpawn;

    [Header("Color Settings")]
    public List<Color> colorPalette = new List<Color>() { Color.red, Color.blue, Color.green, Color.yellow };
    private Color activePaintColor = Color.red;

    // Getters for other scripts
    public InteractionTool CurrentTool => currentTool;
    public GameObject PrefabToSpawn => prefabToSpawn;
    public Color ActivePaintColor => activePaintColor;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Default setup
        if (prefabLibrary.Count > 0) prefabToSpawn = prefabLibrary[0];
        UpdateMenuVisibility();
    }

    // Move(0), Delete(1), Duplicate(2), Color(3), Spawn(4)
    public void SetToolByInt(int toolIndex)
    {
        currentTool = (InteractionTool)toolIndex;
        Debug.Log($"Tool Switched to: {currentTool}");
        UpdateMenuVisibility();
    }

    // Color Sub-Menu Buttons: Red(0), Blue(1), etc.
    public void SetColorByIndex(int index)
    {
        if (index >= 0 && index < colorPalette.Count)
        {
            activePaintColor = colorPalette[index];
            Debug.Log($"Color Selected: {activePaintColor}");
        }
    }

    // Spawn Sub-Menu Buttons: Chair(0), Table(1), etc.
    public void SetPrefabByIndex(int index)
    {
        if (index >= 0 && index < prefabLibrary.Count)
        {
            prefabToSpawn = prefabLibrary[index];
            Debug.Log($"Prefab Selected: {prefabToSpawn.name}");
        }
    }

    // Helper to go back to main menu 
    public void ReturnToMainMenu()
    {
        SetToolByInt((int)InteractionTool.Move);
    }

    private void UpdateMenuVisibility()
    {
        // 1. Hide everything first
        if (mainMenuCanvas) mainMenuCanvas.SetActive(false);
        if (colorMenuCanvas) colorMenuCanvas.SetActive(false);
        if (spawnMenuCanvas) spawnMenuCanvas.SetActive(false);

        // 2. Show based on current tool
        switch (currentTool)
        {
            case InteractionTool.ColorPicker:
                if (colorMenuCanvas) {
                    colorMenuCanvas.SetActive(true);
                    mainMenuCanvas.SetActive(false);
                }
                break;

            case InteractionTool.Spawn:
                if (spawnMenuCanvas) {
                    spawnMenuCanvas.SetActive(true);
                    mainMenuCanvas.SetActive(false);
                } 
                break;

            default:
                // Move, Delete, Duplicate all use the Main Menu
                if (mainMenuCanvas) mainMenuCanvas.SetActive(true);
                break;
        }
    }
}
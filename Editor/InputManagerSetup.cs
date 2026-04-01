using UnityEngine;
using UnityEditor;
using System;
using System.Reflection;

/// <summary>
/// Automatically adds required Input Manager axes for controller support
/// Run this once via Tools > Setup Input Manager
/// </summary>
public class InputManagerSetup : Editor
{
    [MenuItem("Tools/Setup Input Manager for Controller")]
    public static void SetupInputManager()
    {
        // Get SerializedObject for InputManager
        var inputManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/InputManager.asset")[0];
        var obj = new SerializedObject(inputManager);
        var axesProperty = obj.FindProperty("m_Axes");
        
        // Fix Unity's default Jump binding (change from Y to A button)
        FixJumpButton(axesProperty);
        
        // Fix Unity's default Fire1 binding (remove A button conflict)
        FixFireButton(axesProperty);
        
        // Remove old custom axes first
        RemoveAxis(axesProperty, "Right Stick Horizontal");
        RemoveAxis(axesProperty, "Right Stick Vertical");
        RemoveAxis(axesProperty, "DPad Horizontal");
        RemoveAxis(axesProperty, "DPad Vertical");
        RemoveAxis(axesProperty, "Sprint");
        RemoveAxis(axesProperty, "Crouch");
        RemoveAxis(axesProperty, "Prone");
        RemoveAxis(axesProperty, "Slide");
        RemoveAxis(axesProperty, "Calibrate");
        RemoveAxis(axesProperty, "Recalculate");
        
        // Add required axes (try different axis numbers for different controllers)
        // Xbox: Usually 3/4 or 4/5, PlayStation: Usually 2/5 or 3/4
        AddAxis(axesProperty, "Right Stick Horizontal", 3, 0.2f, false);  // Try axis 3
        AddAxis(axesProperty, "Right Stick Vertical", 4, 0.2f, true);     // Try axis 4
        AddAxis(axesProperty, "DPad Horizontal", 5, 0.2f, false);         // Try axis 5
        AddAxis(axesProperty, "DPad Vertical", 6, 0.2f, false);           // Try axis 6
        
        // Add button inputs (Xbox Controller on Windows)
        // 0=A(Jump), 1=B, 2=X, 3=Y, 4=LB, 5=RB, 6=Back, 7=Start, 8=L3, 9=R3
        AddButton(axesProperty, "Sprint", "joystick button 8");      // L3 (Left Stick Click)
        AddButton(axesProperty, "Crouch", "joystick button 1");      // B button
        AddButton(axesProperty, "Prone", "joystick button 2");       // X button  
        AddButton(axesProperty, "Slide", "joystick button 9");       // R3 (Right Stick Click)
        AddButton(axesProperty, "Calibrate", "joystick button 4");   // LB (Left Bumper) - safe choice
        AddButton(axesProperty, "Recalculate", "joystick button 5"); // RB (Right Bumper)
        
        obj.ApplyModifiedProperties();
        
        Debug.Log("Input Manager setup complete! Controller axes and buttons added.");
        Debug.Log("You can now use controller with your game.");
    }
    
    private static void AddAxis(SerializedProperty axesProperty, string name, int axis, float deadZone, bool invert)
    {
        // Check if axis already exists
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            var existingAxis = axesProperty.GetArrayElementAtIndex(i);
            if (existingAxis.FindPropertyRelative("m_Name").stringValue == name)
            {
                Debug.Log($"Axis '{name}' already exists, skipping.");
                return;
            }
        }
        
        axesProperty.InsertArrayElementAtIndex(axesProperty.arraySize);
        var newAxis = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);
        
        newAxis.FindPropertyRelative("m_Name").stringValue = name;
        newAxis.FindPropertyRelative("descriptiveName").stringValue = "";
        newAxis.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
        newAxis.FindPropertyRelative("negativeButton").stringValue = "";
        newAxis.FindPropertyRelative("positiveButton").stringValue = "";
        newAxis.FindPropertyRelative("altNegativeButton").stringValue = "";
        newAxis.FindPropertyRelative("altPositiveButton").stringValue = "";
        newAxis.FindPropertyRelative("gravity").floatValue = 0f;
        newAxis.FindPropertyRelative("dead").floatValue = deadZone;
        newAxis.FindPropertyRelative("sensitivity").floatValue = 1f;
        newAxis.FindPropertyRelative("snap").boolValue = false;
        newAxis.FindPropertyRelative("invert").boolValue = invert;
        newAxis.FindPropertyRelative("type").intValue = 2; // Joystick Axis
        newAxis.FindPropertyRelative("axis").intValue = axis;
        newAxis.FindPropertyRelative("joyNum").intValue = 0; // All Joysticks
        
        Debug.Log($"Added axis: {name} (axis {axis})");
    }
    
    private static void AddButton(SerializedProperty axesProperty, string name, string button)
    {
        // Check if button already exists
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            var existingAxis = axesProperty.GetArrayElementAtIndex(i);
            if (existingAxis.FindPropertyRelative("m_Name").stringValue == name)
            {
                Debug.Log($"Button '{name}' already exists, skipping.");
                return;
            }
        }
        
        axesProperty.InsertArrayElementAtIndex(axesProperty.arraySize);
        var newAxis = axesProperty.GetArrayElementAtIndex(axesProperty.arraySize - 1);
        
        newAxis.FindPropertyRelative("m_Name").stringValue = name;
        newAxis.FindPropertyRelative("descriptiveName").stringValue = "";
        newAxis.FindPropertyRelative("descriptiveNegativeName").stringValue = "";
        newAxis.FindPropertyRelative("negativeButton").stringValue = "";
        newAxis.FindPropertyRelative("positiveButton").stringValue = button;
        newAxis.FindPropertyRelative("altNegativeButton").stringValue = "";
        newAxis.FindPropertyRelative("altPositiveButton").stringValue = "";
        newAxis.FindPropertyRelative("gravity").floatValue = 1000f;
        newAxis.FindPropertyRelative("dead").floatValue = 0.001f;
        newAxis.FindPropertyRelative("sensitivity").floatValue = 1000f;
        newAxis.FindPropertyRelative("snap").boolValue = false;
        newAxis.FindPropertyRelative("invert").boolValue = false;
        newAxis.FindPropertyRelative("type").intValue = 0; // Key or Mouse Button
        newAxis.FindPropertyRelative("axis").intValue = 0;
        newAxis.FindPropertyRelative("joyNum").intValue = 0; // All Joysticks
        
        Debug.Log($"Added button: {name} ({button})");
    }
    
    private static void RemoveAxis(SerializedProperty axesProperty, string name)
    {
        for (int i = axesProperty.arraySize - 1; i >= 0; i--)
        {
            var axis = axesProperty.GetArrayElementAtIndex(i);
            if (axis.FindPropertyRelative("m_Name").stringValue == name)
            {
                axesProperty.DeleteArrayElementAtIndex(i);
                Debug.Log($"Removed old axis: {name}");
            }
        }
    }
    
    private static void FixJumpButton(SerializedProperty axesProperty)
    {
        // Find all Jump entries and fix them
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            var axis = axesProperty.GetArrayElementAtIndex(i);
            if (axis.FindPropertyRelative("m_Name").stringValue == "Jump")
            {
                var positiveButton = axis.FindPropertyRelative("positiveButton");
                var altPositiveButton = axis.FindPropertyRelative("altPositiveButton");
                
                // If it's set to joystick button 3 (Y), change to button 0 (A)
                if (positiveButton.stringValue == "joystick button 3")
                {
                    positiveButton.stringValue = "joystick button 0";
                    Debug.Log("Fixed Jump: Changed from Y button (3) to A button (0)");
                }
                
                // Also check alt button
                if (altPositiveButton.stringValue == "joystick button 3")
                {
                    altPositiveButton.stringValue = "joystick button 0";
                    Debug.Log("Fixed Jump alt: Changed from Y button (3) to A button (0)");
                }
                
                // If no joystick button set, add A button
                if (!positiveButton.stringValue.Contains("joystick") && !altPositiveButton.stringValue.Contains("joystick"))
                {
                    altPositiveButton.stringValue = "joystick button 0";
                    Debug.Log("Added A button (0) as Jump alt button");
                }
            }
        }
    }
    
    private static void FixFireButton(SerializedProperty axesProperty)
    {
        // Find all Fire1 entries and remove joystick button conflicts
        // RT trigger is handled by new Input System in WeaponController
        for (int i = 0; i < axesProperty.arraySize; i++)
        {
            var axis = axesProperty.GetArrayElementAtIndex(i);
            if (axis.FindPropertyRelative("m_Name").stringValue == "Fire1")
            {
                var positiveButton = axis.FindPropertyRelative("positiveButton");
                var altPositiveButton = axis.FindPropertyRelative("altPositiveButton");
                
                // Remove any joystick button mappings (like A button) to avoid conflicts
                if (positiveButton.stringValue.Contains("joystick button"))
                {
                    positiveButton.stringValue = "mouse 0"; // Keep left mouse
                    Debug.Log("Fixed Fire1: Removed joystick button, kept mouse");
                }
                
                // Remove joystick button from alt as well
                if (altPositiveButton.stringValue.Contains("joystick button"))
                {
                    altPositiveButton.stringValue = "";
                    Debug.Log("Removed joystick button from Fire1 alt");
                }
            }
        }
    }
}

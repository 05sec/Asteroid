// Unluck Software © 2013
// http://www.chemicalbliss.com
//
// Material Color Picker v1.0
// Picks color from a GUI texture and applies it to a Shared Material

var targetSharedMaterial : Material;
var colorPickerTexture : Texture2D; 
var guiTextureX : int = 0;
var guiTextureY : int = 0;
var guiTextureWidth : int = 256;
var guiTextureHeight : int = 256;

function OnGUI() {
	var labelStyle : GUIStyle = GUI.skin.label;
    if (GUI.RepeatButton(Rect(guiTextureX, guiTextureY, guiTextureWidth, guiTextureHeight), colorPickerTexture,labelStyle)) { 
        var mousePos : Vector2 = Event.current.mousePosition;
        var textureXpos : int = mousePos.x - guiTextureX+8;
        var textureYpos : int =  mousePos.y - guiTextureY+8;
        var color : Color = colorPickerTexture.GetPixel(textureXpos,colorPickerTexture.height-textureYpos);
        targetSharedMaterial.SetColor("_Tint", color);
    }
}
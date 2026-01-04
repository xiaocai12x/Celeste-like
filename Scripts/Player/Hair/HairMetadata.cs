using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "HairMetadata", menuName = "Data/Hair Metadata")]
public class HairMetadata : ScriptableObject
{
    [System.Serializable]
    public class SpriteOffset
    {
        public string spriteName;
        public Vector2 offset;
    }

    public List<SpriteOffset> offsets = new List<SpriteOffset>();

    public Vector2 GetOffset(string spriteName)
    {
        foreach (var item in offsets)
        {
            if (spriteName.Contains(item.spriteName)) return item.offset;
        }
        return Vector2.zero;
    }
}
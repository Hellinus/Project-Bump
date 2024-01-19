using System.Collections;
using System.Collections.Generic;
using MoreMountains.Tools;
using MoreMountains.CorgiEngine;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace _Bump.Scripts.Tools
{
    public class AnimatorInfo : MonoBehaviour
    {
        protected Character _character;
        protected Text _text;
        
        void Start()
        {
            if(GetComponent<Text>()==null)
            {
                Debug.LogWarning ("FPSCounter requires a GUIText component.");
                return;
            }
            _text = GetComponent<Text>();
        }
        
        void Update()
        {
            if (_character == null)
            {
                _character = FindObjectOfType<Character>();
            }
            AnimatorClipInfo[] m_CurrentClipInfo = _character.GetComponentInChildren<Animator>().GetCurrentAnimatorClipInfo(0);
            _text.text = m_CurrentClipInfo[0].clip.name;
        }
    }
}


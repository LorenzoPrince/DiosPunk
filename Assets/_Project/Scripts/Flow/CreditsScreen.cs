using System;
using UnityEngine;
using UnityEngine.UI;

namespace DiosPunk.Flow
{
    public sealed class CreditsScreen : MonoBehaviour
    {
        [SerializeField] private Button _back;

        public event Action BackRequested;

        private void Awake()
        {
            _back.onClick.AddListener(() => BackRequested?.Invoke());
        }
    }
}

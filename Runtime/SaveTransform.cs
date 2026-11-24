using UnityEngine;

namespace Atthree.Saving
{
    /// <summary>
    /// Objenin konumunu, rotasyonunu (isteğe bağlı ölçeğini) ve Rigidbody'si varsa hızlarını kaydeder.
    /// Kutular, kapılar, düşmanlar, oyuncu... hareket eden her şey için.
    /// </summary>
    [AddComponentMenu("Save System/Save Transform")]
    public class SaveTransform : SaveableBehaviour<SaveTransform.State>
    {
        public struct State
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 scale;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        [Tooltip("Ölçeği de kaydet.")]
        public bool saveScale;
        [Tooltip("Rigidbody varsa hızları da kaydet (yüklenince düşmeye/kaymaya devam eder).")]
        public bool saveVelocity = true;

        protected override State Capture()
        {
            var state = new State
            {
                position = transform.position,
                rotation = transform.rotation,
                scale = transform.localScale,
            };
            if (saveVelocity && TryGetComponent(out Rigidbody body))
            {
                state.velocity = body.linearVelocity;
                state.angularVelocity = body.angularVelocity;
            }
            return state;
        }

        protected override void Restore(State state)
        {
            // CharacterController açıkken konumu kendi tutar ve dışarıdan verilen konumu geri alabilir:
            // ışınlarken kısa süre kapat.
            var controller = GetComponent<CharacterController>();
            bool controllerWasEnabled = controller != null && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;

            // NavMeshAgent de konumu kendi yönetir; ona "Warp" (ışınlan) demek gerekir.
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Warp(state.position);

            transform.SetPositionAndRotation(state.position, state.rotation);
            if (saveScale) transform.localScale = state.scale;

            if (controllerWasEnabled) controller.enabled = true;

            if (TryGetComponent(out Rigidbody body))
            {
                body.position = state.position;
                body.rotation = state.rotation;
                if (!body.isKinematic)
                {
                    body.linearVelocity = saveVelocity ? state.velocity : Vector3.zero;
                    body.angularVelocity = saveVelocity ? state.angularVelocity : Vector3.zero;
                }
            }
        }
    }
}

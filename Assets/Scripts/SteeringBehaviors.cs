using UnityEngine;

/// <summary>
/// SteeringBehaviors — Librería estática de comportamientos de steering.
/// Seek, Flee, Arrive, Wander, Pursue, Evade.
/// Los agentes llaman estos métodos y aplican la fuerza resultante a su movimiento.
/// No requiere componente — es una clase de utilidad estática.
/// </summary>
public static class SteeringBehaviors
{
    // ─────────────────────────────────────────────
    // SEEK — moverse hacia un objetivo
    // Devuelve una fuerza que empuja al agente hacia targetPos.
    // ─────────────────────────────────────────────
    public static Vector3 Seek(Vector3 agentPos, Vector3 targetPos, Vector3 currentVelocity, float maxSpeed)
    {
        Vector3 desired = (targetPos - agentPos).normalized * maxSpeed;
        return desired - currentVelocity;
    }

    // ─────────────────────────────────────────────
    // FLEE — alejarse de un objetivo
    // Opuesto al Seek.
    // ─────────────────────────────────────────────
    public static Vector3 Flee(Vector3 agentPos, Vector3 targetPos, Vector3 currentVelocity, float maxSpeed)
    {
        Vector3 desired = (agentPos - targetPos).normalized * maxSpeed;
        return desired - currentVelocity;
    }

    // ─────────────────────────────────────────────
    // ARRIVE — como Seek pero frena al acercarse
    // slowingRadius: a partir de qué distancia empieza a frenar.
    // ─────────────────────────────────────────────
    public static Vector3 Arrive(Vector3 agentPos, Vector3 targetPos, Vector3 currentVelocity, float maxSpeed, float slowingRadius = 3f)
    {
        Vector3 toTarget = targetPos - agentPos;
        float   distance = toTarget.magnitude;

        if (distance < 0.01f) return -currentVelocity;   // ya llegó, frenar

        float rampedSpeed  = maxSpeed * (distance / slowingRadius);
        float clippedSpeed = Mathf.Min(rampedSpeed, maxSpeed);

        Vector3 desired = toTarget * (clippedSpeed / distance);
        return desired - currentVelocity;
    }

    // ─────────────────────────────────────────────
    // WANDER — movimiento errático natural
    // El agente proyecta un círculo al frente y elige un punto
    // aleatorio en su borde, dando la sensación de deambular.
    // wanderAngle se modifica cada frame y debe guardarse en el agente.
    // ─────────────────────────────────────────────
    public static Vector3 Wander(
        Vector3 agentPos,
        Vector3 currentVelocity,
        float   maxSpeed,
        float   circleDistance,
        float   circleRadius,
        float   angleChange,
        ref float wanderAngle)
    {
        // Actualizar ángulo de wander aleatoriamente
        wanderAngle += Random.Range(-angleChange, angleChange) * Time.deltaTime;

        // Centro del círculo proyectado al frente del agente
        Vector3 circleCenter = currentVelocity.normalized * circleDistance;

        // Punto en el borde del círculo
        Vector3 displacement = new Vector3(
            Mathf.Cos(wanderAngle) * circleRadius,
            0f,
            Mathf.Sin(wanderAngle) * circleRadius);

        Vector3 wanderForce = circleCenter + displacement;

        // Si el agente está quieto, darle una dirección inicial aleatoria
        if (currentVelocity.magnitude < 0.01f)
            wanderForce = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized * maxSpeed;

        return wanderForce;
    }

    // ─────────────────────────────────────────────
    // PURSUE — perseguir prediciendo posición futura
    // Más efectivo que Seek porque anticipa el movimiento del objetivo.
    // ─────────────────────────────────────────────
    public static Vector3 Pursue(
        Vector3 agentPos,
        Vector3 targetPos,
        Vector3 targetVelocity,
        Vector3 currentVelocity,
        float   maxSpeed)
    {
        Vector3 toTarget = targetPos - agentPos;
        float   distance = toTarget.magnitude;

        // Tiempo de predicción: más lejos = predice más adelante
        float   lookAheadTime   = distance / (maxSpeed + targetVelocity.magnitude);
        Vector3 predictedTarget = targetPos + targetVelocity * lookAheadTime;

        return Seek(agentPos, predictedTarget, currentVelocity, maxSpeed);
    }

    // ─────────────────────────────────────────────
    // EVADE — escapar prediciendo posición futura del perseguidor
    // Opuesto al Pursue.
    // ─────────────────────────────────────────────
    public static Vector3 Evade(
        Vector3 agentPos,
        Vector3 pursuerPos,
        Vector3 pursuerVelocity,
        Vector3 currentVelocity,
        float   maxSpeed)
    {
        Vector3 toPursuer      = pursuerPos - agentPos;
        float   distance       = toPursuer.magnitude;
        float   lookAheadTime  = distance / (maxSpeed + pursuerVelocity.magnitude);
        Vector3 predictedPos   = pursuerPos + pursuerVelocity * lookAheadTime;

        return Flee(agentPos, predictedPos, currentVelocity, maxSpeed);
    }

    // ─────────────────────────────────────────────
    // HELPER — Limitar magnitud de un vector
    // ─────────────────────────────────────────────
    public static Vector3 Truncate(Vector3 v, float maxLength)
    {
        return v.magnitude > maxLength ? v.normalized * maxLength : v;
    }
}

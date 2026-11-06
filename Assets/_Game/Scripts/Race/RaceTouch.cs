using UnityEngine;

namespace ShiftJam
{
    /// <summary>
    /// Whether a collider that touched a race line belongs to the vehicle the player drives (Driving.Vehicle). A car
    /// touches with its own body (the collider's rigidbody is the car, or under it); the wooden ToyCar touches with its
    /// hidden physics ball, a separate object, found through ToyCar.All. Anything else (the toy on foot, a loose prop, a
    /// parked car) is not racing.
    /// </summary>
    public static class RaceTouch
    {
        public static bool IsDriven(Collider other)
        {
            Transform vehicle = Driving.Vehicle;
            if (vehicle == null || other == null) return false;
            Rigidbody body = other.attachedRigidbody;
            Transform t = body != null ? body.transform : other.transform;
            if (t == vehicle || t.IsChildOf(vehicle)) return true;
            foreach (var car in ToyCar.All)
                if (car != null && car.Ball == t) return car.transform == vehicle || vehicle.IsChildOf(car.transform);
            return false;
        }
    }
}

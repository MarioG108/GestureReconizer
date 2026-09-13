namespace GestureControl.Core.Models;

/// <summary>
/// Gesture classifications recognized by the system, covering both static and dynamic gesture families
/// defined in Section 8.1 of the architecture document.
/// </summary>
public enum HandGestureType
{
    None = 0,

    // Static Gestures (Discreet actions, tool selection, clicks, activation)
    OpenHand = 1,          // Mano abierta (todas las puntas extendidas)
    Fist = 2,              // Puño cerrado (todos los dedos flexionados)
    Pinch = 3,             // Pinza (contacto o cercanía entre pulgar e índice)
    IndexPoint = 4,        // Dedo índice apuntando (índice extendido, demás flexionados)
    TwoFingersPeace = 5,   // Dos dedos / Paz / Victoria (índice y medio extendidos)
    LateralPalm = 6,       // Palma lateral / perfil

    // Dynamic Gestures (Viewport navigation, orbit, pan, zoom, shortcuts)
    SwipeLeft = 10,
    SwipeRight = 11,
    SwipeUp = 12,
    SwipeDown = 13,
    Push = 14,
    Pull = 15,
    Circle = 16,
    HoldAndDrag = 17
}

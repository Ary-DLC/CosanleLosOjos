// T21 - Textos de las notas de Mateo (el hermano de Lucía, 13 años)
// No se agrega a ningún objeto. Aquí se cambian los textos y las fechas.
// Las palabras entre ~ ~ se dibujan TACHADAS (ej. "~no quiero~").
// Los "\n" son saltos de línea.
public static class MateoNotes
{
    public const int Total = 8;   // 1-2 Seminario, 3-6 Catacumbas, 7-8 El Umbral

    // Fecha escrita a mano arriba de cada nota (índice 0 = nota 1)
    public static readonly string[] Dates =
    {
        "3 de marzo",
        "8 de marzo",
        "15 de marzo",
        "2 de abril",
        "¿mayo?",
        "hoy",
        "—",
        "ahora"
    };

    // 0 = letra limpia, 1 = letra muy desordenada (inclinada, chueca)
    public static readonly float[] Messiness = { 0f, 0.05f, 0.15f, 0.3f, 0.65f, 1f, 1f, 1f };

    public static readonly string[] Texts =
    {
        // 1
        "Lucía: los señores de túnica dijeron que aquí iba a estar bien, que tú no me podías cuidar. " +
        "Pero no me dejan salir. Rezan toda la noche y nos tapan los ojos con vendas. " +
        "Si encuentras esto, NO VENGAS. Tengo mucho miedo.\n— Mateo",

        // 2
        "Hay reglas: no hablar después de las velas, no mirar al Pastor, no correr nunca. " +
        "Un niño corrió ayer y ya no lo vi. Dicen que el Penitente escucha todo, hasta cuando respiras fuerte. " +
        "Ahora camino muy despacito.",

        // 3
        "Hoy me pusieron el primer punto en el ojo. Lloré mucho. " +
        "El Pastor me abrazó y dijo que el dolor limpia y que los valientes no lloran. " +
        "Ya no voy a llorar, Lucía. Te lo prometo.",

        // 4
        "Me escondo en los armarios cuando pasa el Penitente. Es muy alto y no tiene ojos, pero te encuentra igual. " +
        "Una vez se paró justo enfrente y aguanté la respiración hasta que se fue. " +
        "Creo que a ti también te va a buscar.",

        // 5 (con palabras tachadas)
        "Ya no me acuerdo bien de la casa. Ni de tu cara. ~Quiero irme a casa.~ " +
        "Aquí abajo todos son mi familia y nadie me grita. " +
        "El Pastor dice que soy especial, que fui de los primeros en ver de verdad.",

        // 6 (con palabras tachadas)
        "Lucía, te escuché llegar. ~Vete, vete, vete.~ No corras, no sirve. " +
        "No tengas miedo de los puntos, casi no duelen. " +
        "El Pastor dice que tú también eres especial. ~Ayúdame~ " +
        "Te estamos esperando en el centro. Todos.",

        // 7 (El Umbral, nueva: se puede cambiar)
        "Lucía. Ya casi no veo, pero ya no lo necesito. Aquí abajo se oye todo: " +
        "tus pasos, tu respiración, tu miedo. ~Regrésate~ No te escondas. En el centro no duele.",

        // 8 (El Umbral, nueva: se puede cambiar)
        "Ya estás aquí. Siéntate con nosotros en el círculo. El Pastor tiene el hilo listo. " +
        "~Perdóname~ Cierra los ojos, Lucía. Yo te los coso."
    };
}

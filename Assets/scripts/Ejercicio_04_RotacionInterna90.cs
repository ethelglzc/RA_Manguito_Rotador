using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class Ejercicio_04_RotacionInterna90 : MonoBehaviour, IEjercicioRehabilitacion
{
    [Header("Conexion con Interfaz Procedimental")]
    public UIRehabilitacionGenerador interfazUI;

    [Header("Referencias Mando Derecho")]
    public Transform mandoDerecho;
    public NearFarInteractor interactorDerecho;
    public InputActionReference botonCalibracionDerecho;

    [Header("Referencias Mando Izquierdo")]
    public Transform mandoIzquierdo;
    public NearFarInteractor interactorIzquierdo;
    public InputActionReference botonCalibracionIzquierdo;

    [Header("Parametros Prescritos (Configurables)")]
    public int seriesObjetivo = 3;
    public int repeticionesObjetivo = 10;
    public float tiempoDescansoSegundos = 30f;
    public float anguloObjetivo = 70f;
    public int resolucionCurva = 35;

    public int SeriesObjetivo => seriesObjetivo;
    public int RepeticionesObjetivo => repeticionesObjetivo;
    public float TiempoDescansoSegundos => tiempoDescansoSegundos;
    public float AnguloObjetivo => anguloObjetivo;

    [Header("Guias Visuales")]
    public LineRenderer lineaObjetivo;
    public LineRenderer lineaAccion;

    [Header("Grosores de Lineas")]
    public float grosorLineaObjetivo = 0.045f;
    public float grosorLineaAccion = 0.015f;

    [Header("Margen de error y Precision 3D")]
    public float toleranciaDesviacion = 0.12f;
    public float zonaPerfecta = 0.05f;
    public float margenLlegadaBorde = 0.04f;
    public float distanciaMinimaPunto = 0.008f;
    [Header("Calibracion Anatomica")]
    public float desplazamientoInternoHombro = 0.04f; // 4 cm hacia el interior del torso

    public enum EstadoEjercicio
    {
        Configurando,
        CalibrandoHombroDerecho,
        CalibrandoHombroIzquierdo,
        EsperandoPosicionInicial,
        EnEjecucion,
        Descansando,
        FinalizadoTotal
    }
    public EstadoEjercicio estadoActual = EstadoEjercicio.Configurando;

    [Header("Estadisticas Acumuladas")]
    public int serieActual = 0;
    public int repeticionesCompletadas = 0;
    public float precisionMediaGlobal = 0f;

    // Coordenadas fijas del mundo de ambos hombros registradas por el fisioterapeuta
    private Vector3 hombroDerechoPos;
    private Vector3 hombroIzquierdoPos;

    private Transform mandoActivo;
    private NearFarInteractor interactorActivo;
    private bool esLadoDerecho = true;

    private Vector3 posicionInicial;
    private Vector3 centroCodoFijo;
    private float radioAntebrazoReal;
    private Vector3[] puntosArco;
    private List<Vector3> puntosTrazoReal = new List<Vector3>();

    private enum FaseRepeticion { EnOrigen, EnRotacionInferior }
    private FaseRepeticion faseActual = FaseRepeticion.EnOrigen;
    private List<float> precisionesPorRepeticion = new List<float>();
    private Coroutine corrutinaDescanso;

    private void Awake()
    {
        ConfigurarEstiloLineaObjetivo();
        AplicarAcabadoRedondeado(lineaAccion);

        if (lineaObjetivo != null) lineaObjetivo.enabled = false;
        if (lineaAccion != null) lineaAccion.enabled = false;
    }

    private void Start()
    {
        MostrarPanelConfiguracion();
    }

    private void ConfigurarEstiloLineaObjetivo()
    {
        if (lineaObjetivo == null) return;

        AplicarAcabadoRedondeado(lineaObjetivo);

        Gradient gradiente = new Gradient();
        Color colorOrillaBrillante = new Color(1f, 0.98f, 0.75f, 1f);
        Color colorCuerpoCian = new Color(0.10f, 0.72f, 0.95f, 0.65f);

        gradiente.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(colorOrillaBrillante, 0.00f),
                new GradientColorKey(colorOrillaBrillante, 0.06f),
                new GradientColorKey(colorCuerpoCian, 0.14f),
                new GradientColorKey(colorCuerpoCian, 0.86f),
                new GradientColorKey(colorOrillaBrillante, 0.94f),
                new GradientColorKey(colorOrillaBrillante, 1.00f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(1.00f, 0.00f),
                new GradientAlphaKey(1.00f, 0.07f),
                new GradientAlphaKey(0.55f, 0.15f),
                new GradientAlphaKey(0.55f, 0.85f),
                new GradientAlphaKey(1.00f, 0.93f),
                new GradientAlphaKey(1.00f, 1.00f)
            }
        );
        lineaObjetivo.colorGradient = gradiente;

        AnimationCurve curvaGrosor = new AnimationCurve();
        curvaGrosor.AddKey(0.00f, grosorLineaObjetivo * 1.35f);
        curvaGrosor.AddKey(0.08f, grosorLineaObjetivo);
        curvaGrosor.AddKey(0.92f, grosorLineaObjetivo);
        curvaGrosor.AddKey(1.00f, grosorLineaObjetivo * 1.35f);
        lineaObjetivo.widthCurve = curvaGrosor;
        lineaObjetivo.widthMultiplier = 1f;
    }

    private void AplicarAcabadoRedondeado(LineRenderer lr)
    {
        if (lr == null) return;
        lr.numCapVertices = 14;
        lr.numCornerVertices = 14;
    }

    private void OnEnable()
    {
        if (botonCalibracionDerecho != null) botonCalibracionDerecho.action.Enable();
        if (botonCalibracionIzquierdo != null) botonCalibracionIzquierdo.action.Enable();
    }

    private void OnDisable()
    {
        if (botonCalibracionDerecho != null) botonCalibracionDerecho.action.Disable();
        if (botonCalibracionIzquierdo != null) botonCalibracionIzquierdo.action.Disable();
    }

    private void Update()
    {
        // PASO A: Fisioterapeuta registra Hombro Derecho
        if (estadoActual == EstadoEjercicio.CalibrandoHombroDerecho)
        {
            if (DetectarGatilloCualquierMando(out Transform mandoUsado, out NearFarInteractor interactorUsado))
            {
                hombroDerechoPos = mandoUsado.position;
                interactorUsado?.SendHapticImpulse(0.7f, 0.2f);

                estadoActual = EstadoEjercicio.CalibrandoHombroIzquierdo;
                interfazUI?.MostrarModoCalibracionHombros(true, false);
            }
            return;
        }

        // PASO B: Fisioterapeuta registra Hombro Izquierdo
        if (estadoActual == EstadoEjercicio.CalibrandoHombroIzquierdo)
        {
            if (DetectarGatilloCualquierMando(out Transform mandoUsado, out NearFarInteractor interactorUsado))
            {
                hombroIzquierdoPos = mandoUsado.position;
                interactorUsado?.SendHapticImpulse(0.9f, 0.3f);

                estadoActual = EstadoEjercicio.EsperandoPosicionInicial;
                interfazUI?.MostrarModoEsperandoCalibracion();
                interfazUI?.ActualizarDatosEjercicio(
                    "HOMBROS CALIBRADOS\nQuédate en tu lugar, eleva el brazo de lado a 90°, codo a 90° al frente y jala el gatillo",
                    serieActual, seriesObjetivo, 0, repeticionesObjetivo, 0f
                );
            }
            return;
        }

        // PASO C: Paciente inicia la serie jalando el gatillo del brazo a rehabilitar
        if (estadoActual == EstadoEjercicio.EsperandoPosicionInicial)
        {
            if (botonCalibracionDerecho != null && botonCalibracionDerecho.action.WasPressedThisFrame() && mandoDerecho != null)
            {
                mandoActivo = mandoDerecho;
                interactorActivo = interactorDerecho;
                esLadoDerecho = true;
                CalibrarEIniciarSerie();
                return;
            }
            else if (botonCalibracionIzquierdo != null && botonCalibracionIzquierdo.action.WasPressedThisFrame() && mandoIzquierdo != null)
            {
                mandoActivo = mandoIzquierdo;
                interactorActivo = interactorIzquierdo;
                esLadoDerecho = false;
                CalibrarEIniciarSerie();
                return;
            }
        }

        // PASO D: Ejecucion en tiempo real con trayectoria fija en el mundo
        if (estadoActual == EstadoEjercicio.EnEjecucion && mandoActivo != null)
        {
            ActualizarTrazoReal();
            EvaluarCicloRepeticion();
        }
    }

    private bool DetectarGatilloCualquierMando(out Transform mandoUsado, out NearFarInteractor interactorUsado)
    {
        if (botonCalibracionDerecho != null && botonCalibracionDerecho.action.WasPressedThisFrame() && mandoDerecho != null)
        {
            mandoUsado = mandoDerecho;
            interactorUsado = interactorDerecho;
            return true;
        }
        if (botonCalibracionIzquierdo != null && botonCalibracionIzquierdo.action.WasPressedThisFrame() && mandoIzquierdo != null)
        {
            mandoUsado = mandoIzquierdo;
            interactorUsado = interactorIzquierdo;
            return true;
        }

        mandoUsado = null;
        interactorUsado = null;
        return false;
    }

    #region Metodos llamados por la UI (IEjercicioRehabilitacion)

    public void ModificarSeries(int delta)
    {
        seriesObjetivo = Mathf.Max(1, seriesObjetivo + delta);
        interfazUI?.ActualizarValoresConfig(seriesObjetivo, repeticionesObjetivo, tiempoDescansoSegundos, anguloObjetivo);
    }

    public void ModificarRepeticiones(int delta)
    {
        repeticionesObjetivo = Mathf.Max(1, repeticionesObjetivo + delta);
        interfazUI?.ActualizarValoresConfig(seriesObjetivo, repeticionesObjetivo, tiempoDescansoSegundos, anguloObjetivo);
    }

    public void ModificarDescanso(float delta)
    {
        tiempoDescansoSegundos = Mathf.Max(5f, tiempoDescansoSegundos + delta);
        interfazUI?.ActualizarValoresConfig(seriesObjetivo, repeticionesObjetivo, tiempoDescansoSegundos, anguloObjetivo);
    }

    public void ModificarAngulo(float delta)
    {
        anguloObjetivo = Mathf.Clamp(anguloObjetivo + delta, 15f, 90f);
        interfazUI?.ActualizarValoresConfig(seriesObjetivo, repeticionesObjetivo, tiempoDescansoSegundos, anguloObjetivo);
    }

    public void ConfirmarConfiguracionYComenzar()
    {
        serieActual = 1;
        repeticionesCompletadas = 0;
        precisionMediaGlobal = 0f;
        precisionesPorRepeticion.Clear();

        estadoActual = EstadoEjercicio.CalibrandoHombroDerecho;
        interfazUI?.MostrarModoCalibracionHombros(false, false);
    }

    public void CancelarEjercicio()
    {
        if (corrutinaDescanso != null)
        {
            StopCoroutine(corrutinaDescanso);
            corrutinaDescanso = null;
        }

        VibrarMando(0.4f, 0.1f);
        MostrarPanelConfiguracion();
    }

    public void MostrarPanelConfiguracion()
    {
        estadoActual = EstadoEjercicio.Configurando;
        if (lineaObjetivo != null) lineaObjetivo.enabled = false;
        if (lineaAccion != null) lineaAccion.enabled = false;

        interfazUI?.MostrarModoConfiguracion(seriesObjetivo, repeticionesObjetivo, tiempoDescansoSegundos, anguloObjetivo);
    }

    #endregion

    #region Logica de Calibracion con Hombro Fijo y Ejecucion (IR 90/90 - Bajar)

    private void CalibrarEIniciarSerie()
    {
        posicionInicial = mandoActivo.position;
        repeticionesCompletadas = 0;
        faseActual = FaseRepeticion.EnOrigen;

        VibrarMando(0.6f, 0.2f);

        GenerarArcoIR90DesdeHombroFijo();
        IniciarTrazoAccion();

        string ladoTexto = esLadoDerecho ? "Derecho" : "Izquierdo";
        estadoActual = EstadoEjercicio.EnEjecucion;

        if (puntosArco != null && puntosArco.Length > 0)
        {
            Vector3 puntoMedioArco = puntosArco[puntosArco.Length / 2];
            interfazUI?.EnfocarCentroLineaObjetivo(puntoMedioArco);
        }

        interfazUI?.ActualizarDatosEjercicio(
            $"Rotación Interna 90° • Brazo {ladoTexto}\nSin moverte de tu lugar, gira el antebrazo hacia abajo",
            serieActual, seriesObjetivo, repeticionesCompletadas, repeticionesObjetivo, precisionMediaGlobal
        );
    }

    private void GenerarArcoIR90DesdeHombroFijo()
    {
        if (lineaObjetivo == null) return;

        ConfigurarEstiloLineaObjetivo();

        Vector3 hombroSeleccionado = esLadoDerecho ? hombroDerechoPos : hombroIzquierdoPos;
        Vector3 hombroOpuesto = esLadoDerecho ? hombroIzquierdoPos : hombroDerechoPos;

        // Desplazamos el eje unos centimetros hacia el interior del torso para acercarlo a la articulacion real
        Vector3 dirHaciaInterior = hombroOpuesto - hombroSeleccionado;
        dirHaciaInterior.y = 0f;
        if (dirHaciaInterior.sqrMagnitude > 0.001f)
        {
            hombroSeleccionado += dirHaciaInterior.normalized * desplazamientoInternoHombro;
        }

        // Frente exacto del torso calculado perpendicular a la linea entre ambos hombros
        Vector3 ejeHombros = (hombroDerechoPos - hombroIzquierdoPos);
        ejeHombros.y = 0f;

        Vector3 dirAdelante;
        if (ejeHombros.sqrMagnitude > 0.01f)
        {
            dirAdelante = Vector3.Cross(ejeHombros.normalized, Vector3.up).normalized;
            Vector3 haciaMano = posicionInicial - hombroSeleccionado;
            haciaMano.y = 0f;
            if (Vector3.Dot(dirAdelante, haciaMano) < 0f)
                dirAdelante = -dirAdelante;
        }
        else
        {
            dirAdelante = Camera.main != null ? Camera.main.transform.forward : Vector3.forward;
            dirAdelante.y = 0f;
            dirAdelante.Normalize();
        }

        Vector3 vectorHombroAMano = posicionInicial - hombroSeleccionado;
        vectorHombroAMano.y = 0f;
        float proyeccionAntebrazo = Vector3.Dot(vectorHombroAMano, dirAdelante);
        radioAntebrazoReal = Mathf.Clamp(proyeccionAntebrazo, 0.22f, 0.36f);

        centroCodoFijo = posicionInicial - (dirAdelante * radioAntebrazoReal);

        puntosArco = new Vector3[resolucionCurva + 1];
        lineaObjetivo.enabled = true;
        lineaObjetivo.positionCount = puntosArco.Length;

        for (int i = 0; i <= resolucionCurva; i++)
        {
            float t = (float)i / resolucionCurva;
            float rad = (t * anguloObjetivo) * Mathf.Deg2Rad;

            Vector3 direccionPunto = (dirAdelante * Mathf.Cos(rad)) + (Vector3.down * Mathf.Sin(rad));
            Vector3 puntoCalculado = centroCodoFijo + (direccionPunto * radioAntebrazoReal);

            puntosArco[i] = puntoCalculado;
            lineaObjetivo.SetPosition(i, puntoCalculado);
        }
    }

    private void IniciarTrazoAccion()
    {
        if (lineaAccion == null) return;

        puntosTrazoReal.Clear();
        puntosTrazoReal.Add(mandoActivo.position);

        lineaAccion.enabled = true;
        lineaAccion.startWidth = grosorLineaAccion;
        lineaAccion.endWidth = grosorLineaAccion;
        lineaAccion.positionCount = 1;
        lineaAccion.SetPosition(0, mandoActivo.position);
    }

    private void ActualizarTrazoReal()
    {
        Vector3 posActual = mandoActivo.position;
        Vector3 ultimoPunto = puntosTrazoReal[puntosTrazoReal.Count - 1];

        if (Vector3.Distance(posActual, ultimoPunto) >= distanciaMinimaPunto)
        {
            puntosTrazoReal.Add(posActual);

            if (lineaAccion != null)
            {
                lineaAccion.positionCount = puntosTrazoReal.Count;
                lineaAccion.SetPosition(puntosTrazoReal.Count - 1, posActual);
            }
        }

        float desviacion3D = ObtenerDistanciaMinimaAArco(posActual);
        bool estaDesviado = desviacion3D > toleranciaDesviacion;

        interfazUI?.MostrarAlertaDesviacion(estaDesviado);

        if (estaDesviado)
        {
            VibrarMando(0.3f, 0.05f);
        }
    }

    private void EvaluarCicloRepeticion()
    {
        Vector3 posActual = mandoActivo.position;
        Vector3 puntoFinalArco = puntosArco[puntosArco.Length - 1];

        float distAOrigen = Vector3.Distance(posActual, posicionInicial);
        float distAFinal = Vector3.Distance(posActual, puntoFinalArco);

        if (faseActual == FaseRepeticion.EnOrigen && distAFinal <= margenLlegadaBorde)
        {
            faseActual = FaseRepeticion.EnRotacionInferior;
            VibrarMando(0.4f, 0.1f);
            interfazUI?.ActualizarMensajeApoyo("¡Meta inferior alcanzada! Sube el antebrazo al frente lentamente.");
        }
        else if (faseActual == FaseRepeticion.EnRotacionInferior && distAOrigen <= margenLlegadaBorde)
        {
            faseActual = FaseRepeticion.EnOrigen;
            CompletarRepeticion();
        }
    }

    private void CompletarRepeticion()
    {
        repeticionesCompletadas++;

        float precisionRepeticion = CalcularPrecisionTrazo();
        precisionesPorRepeticion.Add(precisionRepeticion);

        float suma = 0f;
        foreach (float p in precisionesPorRepeticion) suma += p;
        precisionMediaGlobal = suma / precisionesPorRepeticion.Count;

        if (repeticionesCompletadas >= repeticionesObjetivo)
        {
            CompletarSerieActual();
        }
        else
        {
            VibrarMando(0.7f, 0.15f);
            interfazUI?.ActualizarDatosEjercicio(
                $"¡Repetición {repeticionesCompletadas} completada!\nSin moverte de tu lugar, baja el antebrazo",
                serieActual, seriesObjetivo, repeticionesCompletadas, repeticionesObjetivo, precisionMediaGlobal
            );
        }
    }

    private void CompletarSerieActual()
    {
        if (lineaAccion != null) lineaAccion.enabled = false;
        if (lineaObjetivo != null) lineaObjetivo.enabled = false;
        interfazUI?.MostrarAlertaDesviacion(false);

        VibrarMando(0.9f, 0.4f);

        if (serieActual >= seriesObjetivo)
        {
            FinalizarEjercicioTotal();
        }
        else
        {
            IniciarDescanso();
        }
    }

    #endregion

    #region Descanso y Finalizacion

    private void IniciarDescanso()
    {
        estadoActual = EstadoEjercicio.Descansando;
        corrutinaDescanso = StartCoroutine(RutinaTimerDescanso());
    }

    private IEnumerator RutinaTimerDescanso()
    {
        float tiempoRestante = tiempoDescansoSegundos;

        while (tiempoRestante > 0)
        {
            int segs = Mathf.CeilToInt(tiempoRestante);
            interfazUI?.MostrarModoDescanso(true, segs);
            interfazUI?.ActualizarDatosEjercicio(
                $"Serie {serieActual} completada.\nBaja el brazo y descansa en tu lugar ({segs}s)",
                serieActual, seriesObjetivo, repeticionesCompletadas, repeticionesObjetivo, precisionMediaGlobal
            );

            yield return new WaitForSeconds(1.0f);
            tiempoRestante -= 1.0f;
        }

        SaltarSiguienteSerie();
    }

    public void SaltarSiguienteSerie()
    {
        if (corrutinaDescanso != null)
            StopCoroutine(corrutinaDescanso);

        interfazUI?.MostrarModoDescanso(false);
        serieActual++;
        estadoActual = EstadoEjercicio.EsperandoPosicionInicial;

        interfazUI?.ActualizarDatosEjercicio(
            $"SERIE {serieActual} DE {seriesObjetivo}\nPermanece en tu lugar, eleva el brazo a 90° y jala el gatillo",
            serieActual, seriesObjetivo, 0, repeticionesObjetivo, precisionMediaGlobal
        );
    }

    private void FinalizarEjercicioTotal()
    {
        estadoActual = EstadoEjercicio.FinalizadoTotal;
        interfazUI?.MostrarModoDescanso(false);
        VibrarMando(1.0f, 0.6f);

        string ladoTexto = esLadoDerecho ? "Derecho" : "Izquierdo";

        interfazUI?.MostrarReporteFinal(
            seriesObjetivo,
            repeticionesObjetivo,
            anguloObjetivo,
            ladoTexto,
            precisionMediaGlobal
        );
    }

    #endregion

    #region Calculos Auxiliares

    private float CalcularPrecisionTrazo()
    {
        if (puntosTrazoReal.Count == 0) return 0f;

        float sumaPorcentajesPuntos = 0f;
        foreach (Vector3 p in puntosTrazoReal)
        {
            float desviacion = ObtenerDistanciaMinimaAArco(p);
            if (desviacion <= zonaPerfecta)
            {
                sumaPorcentajesPuntos += 100f;
            }
            else
            {
                float exceso = desviacion - zonaPerfecta;
                float rangoDegradacion = Mathf.Max(0.01f, toleranciaDesviacion - zonaPerfecta);
                float factor = Mathf.Clamp01(1f - (exceso / rangoDegradacion));
                sumaPorcentajesPuntos += factor * 100f;
            }
        }
        return sumaPorcentajesPuntos / puntosTrazoReal.Count;
    }

    private float ObtenerDistanciaMinimaAArco(Vector3 punto)
    {
        if (puntosArco == null || puntosArco.Length == 0) return 0f;

        float distanciaMinima = float.MaxValue;
        for (int i = 0; i < puntosArco.Length - 1; i++)
        {
            float dist = CalcularDistanciaASegmento(punto, puntosArco[i], puntosArco[i + 1]);
            if (dist < distanciaMinima)
                distanciaMinima = dist;
        }
        return distanciaMinima;
    }

    private float CalcularDistanciaASegmento(Vector3 C, Vector3 A, Vector3 B)
    {
        Vector3 AB = B - A;
        Vector3 AC = C - A;
        float longitudCuadrada = AB.sqrMagnitude;

        if (longitudCuadrada == 0) return Vector3.Distance(C, A);

        float t = Mathf.Clamp01(Vector3.Dot(AC, AB) / longitudCuadrada);
        Vector3 proyeccion = A + t * AB;
        return Vector3.Distance(C, proyeccion);
    }

    private void VibrarMando(float intensidad, float duracion)
    {
        if (interactorActivo != null)
        {
            interactorActivo.SendHapticImpulse(intensidad, duracion);
        }
    }

    #endregion

        public void RecalibrarEjercicio()
    {
        if (corrutinaDescanso != null)
        {
            StopCoroutine(corrutinaDescanso);
            corrutinaDescanso = null;
        }

        if (lineaObjetivo != null) lineaObjetivo.enabled = false;
        if (lineaAccion != null) lineaAccion.enabled = false;
        puntosTrazoReal.Clear();
        repeticionesCompletadas = 0;

        VibrarMando(0.4f, 0.12f);
        estadoActual = EstadoEjercicio.EsperandoPosicionInicial;
        interfazUI?.MostrarModoEsperandoCalibracion();
    }
}
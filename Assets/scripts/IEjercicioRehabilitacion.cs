public interface IEjercicioRehabilitacion
{
    int SeriesObjetivo { get; }
    int RepeticionesObjetivo { get; }
    float TiempoDescansoSegundos { get; }
    float AnguloObjetivo { get; }

    void ModificarSeries(int delta);
    void ModificarRepeticiones(int delta);
    void ModificarDescanso(float delta);
    void ModificarAngulo(float delta);

    void ConfirmarConfiguracionYComenzar();
    void CancelarEjercicio();
    void SaltarSiguienteSerie();
    void MostrarPanelConfiguracion();
    void RecalibrarEjercicio();
}


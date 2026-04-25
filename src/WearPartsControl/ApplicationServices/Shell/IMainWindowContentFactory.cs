namespace WearPartsControl.ApplicationServices.Shell;

public interface IMainWindowContentFactory
{
    object Create(Type contentType);
}
namespace TECkit_Mapping_Editor
{
    public class MyDataGridView : System.Windows.Forms.DataGridView
    {
        public bool IsVerticalScrollBarVisible
        {
            get
            {
                return VerticalScrollBar.Visible;
            }
        }
    }
}

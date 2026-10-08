namespace SDRSharp.IdleSignalCanceller;
public sealed class IdleSignalPanel : UserControl
{
    private readonly IdleAudioProcessor _processor;
    private readonly CheckBox _enable = new() { Name="enableCheckBox",Text="キャンセラーを有効にする",AutoSize=true,Margin=new Padding(6,10,6,10) };
    private readonly Label _status = new() { Name="statusLabel",AutoSize=false,Height=50,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(8),Margin=new Padding(6) };
    private readonly Label _metrics = new() {AutoSize=true,Margin=new Padding(6)};
    private readonly System.Windows.Forms.Timer _timer = new() {Interval=100};
    private readonly NumericUpDown _center, _width, _threshold, _acquire, _release, _level;

    public IdleSignalPanel(IdleAudioProcessor processor,Color foreground,Color background)
    {
        _processor=processor;AutoScaleMode=AutoScaleMode.Dpi;Font=new Font("Yu Gothic UI",9);ForeColor=foreground;BackColor=background;
        MinimumSize=new Size(240,380);Size=new Size(300,420);Padding=new Padding(4);
        var layout=new TableLayoutPanel {Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,RowCount=0,BackColor=background};
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));
        Controls.Add(layout);
        AddFull(layout,_enable);AddFull(layout,_status);AddFull(layout,_metrics);
        _center=AddSetting(layout,"中心周波数 (Hz)",500,3000,1500,25);
        _width=AddSetting(layout,"許容幅 ± (Hz)",25,150,50,25);
        _threshold=AddSetting(layout,"検出閾値 (dB)",3,30,7,.5m,1);
        _acquire=AddSetting(layout,"検出継続 (ms)",80,1000,160,40);
        _release=AddSetting(layout,"解除時間 (ms)",40,400,80,40);
        _level=AddSetting(layout,"最小レベル (dBFS)",-100,-10,-65,5);
        var note=new Label {AutoSize=true,MaximumSize=new Size(285,0),Text="NFM用・約1.5 kHzの空線信号を検出すると音声全体をミュートします。\n閾値を下げるほど高感度になります。通話も消える場合は閾値を上げてください。",Margin=new Padding(6,12,6,6)};AddFull(layout,note);
        _enable.CheckedChanged+=(_,_)=>{processor.SetEnabled(_enable.Checked);RefreshStatus();};
        foreach(var numeric in new[]{_center,_width,_threshold,_acquire,_release,_level})numeric.ValueChanged+=(_,_)=>ApplySettings();
        _timer.Tick+=(_,_)=>RefreshStatus();_timer.Start();RefreshStatus();
    }

    private void ApplySettings()
    {
        _processor.Configure(new IdleSignal.Core.DetectorSettings {CenterHz=(double)_center.Value,ToleranceHz=(double)_width.Value,ThresholdDb=(double)_threshold.Value,AcquireMs=(int)_acquire.Value,ReleaseMs=(int)_release.Value,MinimumLevelDb=(double)_level.Value});
        RefreshStatus();
    }

    public void RefreshStatus()
    {
        var state=_processor.Snapshot;
        var idle=_processor.UserEnabled && _processor.Receiving && _processor.SupportedMode && state.IsIdle;
        _status.Text=!_processor.UserEnabled ? "停止中（音声を通過）" : !_processor.Receiving ? "受信停止中" : !_processor.SupportedMode ? "NFMを選択してください（音声を通過）" : _processor.Error is {} error ? "検出できません："+error : idle ? "空線信号あり\nミュート中" : "監視中（音声を通過）";
        _status.BackColor=idle ? Color.FromArgb(32,105,78) : Color.FromArgb(62,67,77);_status.ForeColor=Color.White;
        _metrics.Text=$"トーン突出度 {state.ProminenceDb:F1} dB  /  レベル {state.LevelDb:F1} dBFS";
    }

    private static void AddFull(TableLayoutPanel layout,Control control)
    {
        var row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));layout.Controls.Add(control,0,row);layout.SetColumnSpan(control,2);
    }
    private static NumericUpDown AddSetting(TableLayoutPanel layout,string text,decimal min,decimal max,decimal value,decimal step,int decimals=0)
    {
        var row=layout.RowCount++;layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(new Label {Text=text,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(6,7,4,7)},0,row);
        var editor=new NumericUpDown {Minimum=min,Maximum=max,Value=value,Increment=step,DecimalPlaces=decimals,Dock=DockStyle.Fill,Margin=new Padding(4,5,6,5)};layout.Controls.Add(editor,1,row);return editor;
    }
    protected override void Dispose(bool disposing)
    {
        if(disposing){_timer.Stop();_timer.Dispose();}
        base.Dispose(disposing);
    }
}

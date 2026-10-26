import {guidePlateLabels,type Order} from './prototype-model';

/** Planned cargo configuration only; never present it as device feedback. */
export function OrderCargoSummary({order}:{order:Order}) {
  if(order.machine!=='QC')return null;
  return <section className="cargo-summary" aria-label="岸桥工单箱组与导板配置">
    <p className="muted">工单配置 · 非设备反馈</p>
    <p>导板工况：<strong>{order.guidePlateMode?guidePlateLabels[order.guidePlateMode]:'旧工单未配置'}</strong></p>
    {order.containers?<ol>{order.containers.map((box,i)=><li key={i}>
      <strong>{i+1} 号箱 · {box.containerId}</strong>
      <span>{box.size==='FT20'?'20 尺':'40 尺'} · {box.grossMassKg.toLocaleString('zh-CN')} kg</span>
    </li>)}</ol>:<p className="muted">旧工单无箱组信息，历史快照未改写。</p>}
  </section>;
}

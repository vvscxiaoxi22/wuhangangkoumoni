export type Machine = 'YC' | 'QC';
export type Role = 'student' | 'teacher';
export type Category = '单项练习' | '联动练习' | '作业练习' | '能力提升' | '故障排除' | '自由练习';
export type Mode = 'Local' | 'Remote';
export type Page = 'home' | 'tasks' | 'records' | 'monitor' | 'tos' | 'digital' | 'settings' | 'guide';
export interface Template { id: string; name: string; category: Category; description: string; targets: string[]; }
export type GuidePlateMode = 'AutoByWorkflow' | 'AllUp' | 'AllDown' | 'SeaSide' | 'LandSide';
export interface ContainerBinding { containerId: string; size: 'FT20' | 'FT40'; grossMassKg: number; }
export const guidePlateLabels: Record<GuidePlateMode,string> = {
  AutoByWorkflow: '按作业流程', AllUp: '全部抬起', AllDown: '全部放下',
  SeaSide: '海侧工况', LandSide: '陆侧工况',
};
export interface Order {
  id: string; machine: Machine; name: string; templateId: string; category: Category;
  target: string; minutes: number; size: string; lane: number; vehicle: string;
  source: string; destination: string; assignee: string; mode: Mode; assist: string;
  status: 'draft' | 'published' | 'withdrawn'; faults: string[];
  externalId?: string;
  // Optional only for reading V1 prototype cache. New QC runs require explicit configuration.
  guidePlateMode?: GuidePlateMode;
  containers?: ContainerBinding[];
}
export interface RunRecord { id: string; order: Order; student: string; className: string; major: string; startedAt: string; endedAt: string; elapsed: number; status: 'completed' | 'terminated'; events: string[]; }
export interface Settings { input: string; station: string; deployment: string; ip: string; rack: number; slot: number; cycle: number; }
export interface Workspace { schema: 1; orders: Order[]; records: RunRecord[]; settings: Settings; }
export const machineName = (m: Machine) => m === 'YC' ? '场桥' : '岸桥';
export const categories: Category[] = ['单项练习','联动练习','作业练习','能力提升','故障排除','自由练习'];
export const modes = { Local: '本地驾驶室', Remote: 'ROCS 远程操控' };
export const faultNames = ['电源故障','吊具左右倾','吊具前后倾','平面回转','吊具泵','小车机构','起升 / 大车','夹轮器'];
export function catalogue(m: Machine): Template[] {
  const yc = m === 'YC';
  const rows: [string, Category, string[], string][] = [
    ['大车移动','单项练习',['-50 m','-30 m','30 m','50 m'],'制动释放、平稳启动与目标位置停车'],
    ['小车移动','单项练习',yc ? ['15 m','35 m','45 m'] : ['30 m','60 m','90 m'],'练习小车加减速与定位'],
    ['吊具起升','单项练习',yc ? ['5 m','10 m','20 m'] : ['10 m','20 m','30 m'],'起升、下降与指定高度停止'],
    [yc ? '吊具回旋' : '吊具倾斜','单项练习',yc ? ['左旋 3°','右旋 3°','自动回零'] : ['左倾 2°','右倾 2°','前倾 2°','后倾 2°'],'识别实际姿态反馈与回零状态'],
    ...(!yc ? [['岸桥俯仰','单项练习',['0° → 80°'],'岸桥专属俯仰机构练习'] as [string,Category,string[],string]] : []),
    ['大车 / 小车联动','联动练习',yc ? ['50 / 15 m','-50 / 35 m'] : ['50 / 30 m','-50 / 60 m'],'两机构协调定位'],
    ['大车 / 起升联动','联动练习',['50 / 10 m','-50 / 20 m'],'行走与起升联动'],
    ['小车 / 起升联动','联动练习',yc ? ['15 / 10 m','35 / 20 m'] : ['50 / 10 m','30 / 20 m'],'控制轨迹与安全净空'],
    ['大车 / 小车 / 起升联动','联动练习',yc ? ['50 / 15 / 10 m','-50 / 35 / 20 m'] : ['50 / 30 / 10 m','-50 / 40 / 20 m'],'三机构协同控制'],
    [yc ? '收箱作业' : '卸船作业','作业练习',['标准作业'],'从源箱位取箱，转运并落至目标箱位'],
    [yc ? '发箱作业' : '装船作业','作业练习',['标准作业'],'确认箱号、车道、车辆与目的位置'],
    ...(yc ? ['定点取箱与扫桩','大车直线行驶','取箱与对位','障碍摆箱','综合连续作业'] : ['过桩练习','定点练习','障碍物定点','综合连续作业']).map(n => [n,'能力提升',['标准训练','进阶训练'],'按项目要求完成精准操作'] as [string,Category,string[],string]),
    ...faultNames.map(n => [!yc && n==='小车机构' ? '俯仰 / 小车' : n,'故障排除',['诊断训练'],'选择故障、查看资料、记录诊断并复位'] as [string,Category,string[],string]),
    ['自由练习','自由练习',['自主练习'],'独立练习，不占用教师工单'],
  ];
  return rows.map(([name,category,targets,description],i)=>({id:m+'-TPL-'+String(i+1).padStart(2,'0'),name,category,targets,description}));
}
export function makeOrder(m: Machine, t = catalogue(m)[0]): Order {
  const source=m==='YC'?'YC-L01':'VSL-01/B53/R04/T82';
  const destination=m==='YC'?'A01/B03/R02/T01':'QC-L01';
  const reverse=t.category==='作业练习'&&(t.name==='装船作业'||t.name==='发箱作业');
  return {id:crypto.randomUUID(),machine:m,name:t.name,templateId:t.id,category:t.category,target:t.targets[0],minutes:15,size:'FT40',lane:1,vehicle:'AGV-01',source:reverse?destination:source,destination:reverse?source:destination,assignee:'全体学员',mode:'Local',assist:'手动',status:'draft',faults:t.category==='故障排除'?[t.name]:[],...(m==='QC'?{guidePlateMode:'AutoByWorkflow' as GuidePlateMode,containers:resizeContainers('FT40')}: {})};
}
export function resizeContainers(size:string, previous:ContainerBinding[]=[]):ContainerBinding[] {
  const count=size==='Twin20'?2:1;
  return Array.from({length:count},(_,i)=>({containerId:previous[i]?.containerId??'DEMO-QC-0'+(i+1),grossMassKg:previous[i]?.grossMassKg??20000,size:size==='FT40'?'FT40':'FT20'}));
}
export function validateOrder(o: Order, requireConfigured=false): string[] {
  if(!o||!['YC','QC'].includes(o.machine)||!['id','name','templateId','target','size','vehicle','source','destination','assignee'].every(k=>typeof o[k as keyof Order]==='string')||!Array.isArray(o.faults)||o.faults.some(f=>typeof f!=='string')||!['Local','Remote'].includes(o.mode)||!['draft','published','withdrawn'].includes(o.status)||!['手动','自动化 + 人工辅助'].includes(o.assist)) return ['任务结构或枚举无效'];
  const t = catalogue(o.machine).find(t=>t.id===o.templateId);
  const errors: string[]=[];
  if(!o.name.trim()) errors.push('请输入任务名称');
  if(!t || t.category!==o.category || !t.targets.includes(o.target)) errors.push('训练项目或目标参数不匹配');
  if(!Number.isInteger(o.minutes)||o.minutes<1||o.minutes>180) errors.push('练习时长须为 1–180 分钟整数');
  if(!Number.isInteger(o.lane)||o.lane<1||o.lane>(o.machine==='YC'?5:9)) errors.push('车道不在当前机型范围内');
  if(!(o.machine==='YC'?['FT20','FT40']:['FT20','FT40','Twin20']).includes(o.size)) errors.push('当前机型不支持此箱型');
  if(!o.source.trim()||!o.destination.trim()||o.source.trim()===o.destination.trim()) errors.push('源位置和目标位置须有效且不同');
  if(!o.assignee.trim()) errors.push('请选择学员范围');
  if(o.category==='故障排除'&&!o.faults.length) errors.push('至少选择一种故障');
  if(o.machine==='YC'&&(o.guidePlateMode!==undefined||o.containers!==undefined)) errors.push('场桥原型不能携带岸桥专属导板 / 箱组配置');
  if(o.machine==='QC') {
    if(requireConfigured&&(o.guidePlateMode===undefined||o.containers===undefined)) errors.push('旧岸桥工单缺少箱组或导板配置，请教师复制并补全后发布');
    if(o.guidePlateMode!==undefined&&!Object.hasOwn(guidePlateLabels,o.guidePlateMode)) errors.push('导板工况无效');
    if(o.containers!==undefined) {
      const boxes=o.containers;
      if(!Array.isArray(boxes)||boxes.length!==(o.size==='Twin20'?2:1)) errors.push('双20尺必须绑定两只箱，单箱模式只允许一只箱');
      else {
        if(boxes.some(b=>!b||typeof b.containerId!=='string'||!b.containerId.trim()||b.containerId.length>64||b.size!==(o.size==='FT40'?'FT40':'FT20')||!Number.isSafeInteger(b.grossMassKg)||b.grossMassKg<=0)) errors.push('箱号不能为空，箱型须匹配吊具，质量须为正整数 kg');
        else if(new Set(boxes.map(b=>b.containerId.trim().toUpperCase())).size!==boxes.length) errors.push('双20尺的两只集装箱必须使用不同箱号');
      }
    }
  }
  return errors;
}
export function emptyWorkspace(): Workspace { return {schema:1,orders:[],records:[],settings:{input:'键盘',station:'DEMO-01',deployment:'电脑模拟',ip:'192.168.0.10',rack:0,slot:1,cycle:20}}; }
export const storageKey='port-training-prototype-v1';
export function readWorkspace(): Workspace {
  const raw=localStorage.getItem(storageKey);
  if(!raw) return emptyWorkspace();
  const x: unknown=JSON.parse(raw);
  if(!x||typeof x!=='object') throw new Error('原型缓存格式无效');
  const w=x as Workspace;
  if(w.schema!==1||!Array.isArray(w.orders)||!Array.isArray(w.records)||!w.settings) throw new Error('原型缓存版本不匹配');
  if(!['input','station','deployment','ip'].every(k=>typeof w.settings[k as keyof Settings]==='string')||!['rack','slot','cycle'].every(k=>Number.isFinite(w.settings[k as keyof Settings]))) throw new Error('原型设置结构无效');
  if(w.orders.some(o=>!o||!['YC','QC'].includes(o.machine)||typeof o.id!=='string'||typeof o.name!=='string'||!Array.isArray(o.faults)||validateOrder(o).length)||w.records.some(r=>!r||typeof r.id!=='string'||!Array.isArray(r.events)||typeof r.student!=='string'||!Number.isFinite(r.elapsed)||!r.order||validateOrder(r.order).length)) throw new Error('原型缓存内容校验失败');
  return w;
}
export const duration=(s:number)=>Math.floor(s/60).toString().padStart(2,'0')+':'+Math.floor(s%60).toString().padStart(2,'0');
export function exportRecords(records: RunRecord[]) {
  const cell=(v:unknown)=>{let s=String(v);if(/^[\s]*[=+@-]/.test(s))s="'"+s;return '"'+s.replaceAll('"','""')+'"';};
  const rows=[['运行编号','学员','机型','项目','开始时间','用时秒','状态','评分可信度'],...records.map(r=>[r.id,r.student,machineName(r.order.machine),r.order.name,r.startedAt,r.elapsed,r.status,'Unscored / 原型演示'])];
  const url=URL.createObjectURL(new Blob(['\uFEFF'+rows.map(row=>row.map(cell).join(',')).join('\r\n')],{type:'text/csv;charset=utf-8'}));
  const a=document.createElement('a');a.href=url;a.download='港口实训-演示记录.csv';a.click();setTimeout(()=>URL.revokeObjectURL(url),1000);
}
